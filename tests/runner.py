#!/usr/bin/env python3
"""
Generic interactive test runner with live log monitoring.
Loads a test plan from tests/plans/<name>.py and serves a web UI.

Setup:  pip install -r tests/requirements.txt  (or use the GameLogMCP uv venv)
Run:    python tests/runner.py --plan cps
Open:   http://localhost:8765

Plan modules must define:
    PLAN_NAME    str          — display name shown in the UI
    STEPS        list[dict]   — list of step dicts (see below)
    LOG_PREFIXES tuple[str]   — log line filter (optional, has default)

Each step dict:
    id            str          — e.g. "1.1"
    section       str          — section heading
    action        str          — what to do in the game
    expected      str          — what to look for / verify
    auto_patterns list[str]    — regex patterns; matching log lines are highlighted (optional)
"""

import argparse
import asyncio
import glob as glob_mod
import importlib.util
import json
import os
import re
import sys
from contextlib import asynccontextmanager
from dataclasses import dataclass, field
from enum import Enum
from typing import Optional

from fastapi import FastAPI, Request
from fastapi.responses import HTMLResponse, JSONResponse, StreamingResponse

# ---------------------------------------------------------------------------
# Plan loading
# ---------------------------------------------------------------------------

PLANS_DIR            = os.path.join(os.path.dirname(__file__), "plans")
DEFAULT_LOG_PREFIXES = ("[CalradianPostalService]", "[BloodlineProgression]")
MANGOHUD_FILE        = os.path.join(os.path.dirname(__file__), "mangohud_status.txt")


def _discover_plans() -> list[str]:
    return sorted(
        f[:-3] for f in os.listdir(PLANS_DIR)
        if f.endswith(".py") and not f.startswith("_")
    )


def _load_plan(name: str):
    path = os.path.join(PLANS_DIR, f"{name}.py")
    if not os.path.exists(path):
        available = ", ".join(_discover_plans()) or "(none)"
        sys.exit(f"Plan '{name}' not found. Available: {available}")
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        sys.exit(f"Could not load plan '{name}' from {path}")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _parse_args():
    plans  = _discover_plans()
    parser = argparse.ArgumentParser(description="Interactive test runner")
    parser.add_argument(
        "--plan", "-p",
        default=plans[0] if len(plans) == 1 else None,
        help=f"Plan to load. Available: {', '.join(plans)}",
    )
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()
    if args.plan is None:
        parser.error(f"--plan required. Available: {', '.join(plans)}")
    return args


# ---------------------------------------------------------------------------
# Step model
# ---------------------------------------------------------------------------

class Status(str, Enum):
    PENDING = "pending"
    PASS    = "pass"
    FAIL    = "fail"
    SKIP    = "skip"


@dataclass
class Step:
    id:            str
    section:       str
    action:        str
    expected:      str
    status:        Status    = Status.PENDING
    detected_logs: list[str] = field(default_factory=list)
    auto_patterns: list[str] = field(default_factory=list)
    # auto_pass: mark this step passed automatically when patterns match
    auto_pass:     bool      = False
    # auto_pass_all: require ALL patterns to have matched (default: any one is enough)
    auto_pass_all: bool      = False


def _build_steps(raw: list[dict]) -> list[Step]:
    return [
        Step(
            id            = r["id"],
            section       = r["section"],
            action        = r["action"],
            expected      = r["expected"],
            auto_patterns = r.get("auto_patterns", []),
            auto_pass     = r.get("auto_pass", False),
            auto_pass_all = r.get("auto_pass_all", False),
        )
        for r in raw
    ]


def _auto_pass_satisfied(step: Step) -> bool:
    """Return True if the step's auto-pass condition has been met."""
    if not step.auto_patterns or not step.detected_logs:
        return False
    if step.auto_pass_all:
        # Every pattern must appear in at least one detected log line
        return all(
            any(re.search(p, line, re.IGNORECASE) for line in step.detected_logs)
            for p in step.auto_patterns
        )
    return len(step.detected_logs) > 0  # any match is enough


# ---------------------------------------------------------------------------
# Log reading
# ---------------------------------------------------------------------------

LOG_DIR = os.path.join(os.path.dirname(__file__), "..", "GameLogs")


def _latest_log() -> Optional[str]:
    files = glob_mod.glob(os.path.join(LOG_DIR, "rgl_log_*.txt"))
    return max(files, key=os.path.getmtime) if files else None


def _read_mod_lines_since(path: str, pos: int, prefixes: tuple) -> tuple[list[str], int]:
    with open(path, encoding="utf-8", errors="replace") as f:
        f.seek(pos)
        lines   = [line.rstrip() for line in f if any(p in line for p in prefixes)]
        new_pos = f.tell()
    return lines, new_pos


# ---------------------------------------------------------------------------
# App state (populated after arg parsing at __main__)
# ---------------------------------------------------------------------------

_plan_name:    str         = "Test Runner"
_log_prefixes: tuple       = DEFAULT_LOG_PREFIXES
_steps:        list[Step]  = []
_state:        dict        = {}
_sse_clients:  list[asyncio.Queue] = []


def _init_state() -> None:
    _state.update({
        "steps":    _steps,
        "cur_idx":  0,
        "log_tail": [],
        "log_path": None,
        "log_pos":  0,
    })


def _snapshot() -> dict:
    steps = _state["steps"]
    summary = {
        "total":   len(steps),
        "passed":  sum(1 for s in steps if s.status == Status.PASS),
        "failed":  sum(1 for s in steps if s.status == Status.FAIL),
        "skipped": sum(1 for s in steps if s.status == Status.SKIP),
        "pending": sum(1 for s in steps if s.status == Status.PENDING),
    }
    return {
        "plan_name": _plan_name,
        "steps": [
            {
                "id":            s.id,
                "section":       s.section,
                "action":        s.action,
                "expected":      s.expected,
                "status":        s.status,
                "detected_logs": s.detected_logs[-6:],
                "auto_pass":     s.auto_pass,
            }
            for s in steps
        ],
        "cur_idx":  _state["cur_idx"],
        "log_tail": _state["log_tail"][-30:],
        "summary":  summary,
    }


async def _broadcast(event: str, data: dict) -> None:
    msg = f"event: {event}\ndata: {json.dumps(data)}\n\n"
    for q in list(_sse_clients):
        await q.put(msg)


def _write_mangohud() -> None:
    try:
        steps   = _state["steps"]
        cur     = steps[_state["cur_idx"]]
        total   = len(steps)
        passed  = sum(1 for s in steps if s.status == Status.PASS)
        failed  = sum(1 for s in steps if s.status == Status.FAIL)
        skipped = sum(1 for s in steps if s.status == Status.SKIP)
        pending = sum(1 for s in steps if s.status == Status.PENDING)
        mode    = "AUTO" if cur.auto_pass else "MANUAL"
        status  = cur.status.upper() if cur.status != Status.PENDING else f"WAITING ({mode})"
        with open(MANGOHUD_FILE, "w") as f:
            f.write(
                f"{_plan_name}  {passed}✓ {failed}✗ {skipped}— {pending}○\n"
                f"Step {cur.id}/{total}: {cur.action[:48]}\n"
                f"{status}"
            )
    except Exception:
        pass


async def _log_poll_loop() -> None:
    while True:
        await asyncio.sleep(2)
        try:
            path = _latest_log()
            if not path:
                continue
            if path != _state["log_path"]:
                _state["log_path"] = path
                _state["log_pos"]  = 0

            lines, new_pos = _read_mod_lines_since(path, _state["log_pos"], _log_prefixes)
            _state["log_pos"] = new_pos
            if not lines:
                continue

            _state["log_tail"].extend(lines)
            _state["log_tail"] = _state["log_tail"][-200:]

            cur           = _state["steps"][_state["cur_idx"]]
            state_changed = False
            for line in lines:
                for pattern in cur.auto_patterns:
                    if re.search(pattern, line, re.IGNORECASE):
                        if line not in cur.detected_logs:
                            cur.detected_logs.append(line)
                            state_changed = True
                        break

            # Auto-advance if this step is configured to pass on log detection
            if state_changed and cur.auto_pass and cur.status == Status.PENDING:
                if _auto_pass_satisfied(cur):
                    cur.status = Status.PASS
                    steps = _state["steps"]
                    for j in range(_state["cur_idx"] + 1, len(steps)):
                        if steps[j].status == Status.PENDING:
                            _state["cur_idx"] = j
                            break

            await _broadcast("logs", {"lines": lines})
            if state_changed:
                await _broadcast("state", _snapshot())
                _write_mangohud()

        except Exception as e:
            print(f"[runner] Log poll error: {e}")


# ---------------------------------------------------------------------------
# FastAPI app
# ---------------------------------------------------------------------------

@asynccontextmanager
async def lifespan(app: FastAPI):
    _init_state()
    _write_mangohud()
    task = asyncio.create_task(_log_poll_loop())
    yield
    task.cancel()


app = FastAPI(lifespan=lifespan)


@app.get("/", response_class=HTMLResponse)
async def index():
    return HTMLResponse(_HTML)


@app.get("/state")
async def get_state():
    return JSONResponse(_snapshot())


@app.get("/stream")
async def stream(request: Request):
    q: asyncio.Queue[str] = asyncio.Queue()
    _sse_clients.append(q)

    async def generate():
        try:
            yield f"event: state\ndata: {json.dumps(_snapshot())}\n\n"
            while True:
                if await request.is_disconnected():
                    break
                try:
                    msg = await asyncio.wait_for(q.get(), timeout=15.0)
                    yield msg
                except asyncio.TimeoutError:
                    yield ": keepalive\n\n"
        finally:
            try:
                _sse_clients.remove(q)
            except ValueError:
                pass

    return StreamingResponse(generate(), media_type="text/event-stream",
                             headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"})


@app.post("/steps/{step_id}/{result}")
async def set_result(step_id: str, result: str):
    if result not in ("pass", "fail", "skip"):
        return JSONResponse({"error": "invalid result"}, status_code=400)
    steps = _state["steps"]
    for i, step in enumerate(steps):
        if step.id == step_id:
            step.status = Status(result)
            for j in range(i + 1, len(steps)):
                if steps[j].status == Status.PENDING:
                    _state["cur_idx"] = j
                    break
            await _broadcast("state", _snapshot())
            _write_mangohud()
            return JSONResponse({"ok": True})
    return JSONResponse({"error": "not found"}, status_code=404)


@app.post("/navigate/{step_id}")
async def navigate(step_id: str):
    for i, step in enumerate(_state["steps"]):
        if step.id == step_id:
            _state["cur_idx"] = i
            await _broadcast("state", _snapshot())
            _write_mangohud()
            return JSONResponse({"ok": True})
    return JSONResponse({"error": "not found"}, status_code=404)


# ---------------------------------------------------------------------------
# HTML UI
# ---------------------------------------------------------------------------

_HTML = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Test Runner</title>
<style>
:root {
  --bg:      #12141c;
  --surface: #1a1d2e;
  --card:    #232640;
  --accent:  #e06c75;
  --pass:    #98c379;
  --fail:    #e06c75;
  --skip:    #7a8499;
  --cyan:    #56b6c2;
  --text:    #abb2bf;
  --bright:  #dde1e7;
}
* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: 'Segoe UI', system-ui, sans-serif; background: var(--bg); color: var(--text); height: 100vh; display: flex; flex-direction: column; overflow: hidden; }

header { background: var(--surface); padding: 10px 18px; display: flex; align-items: center; gap: 14px; border-bottom: 1px solid #2a2d3e; flex-shrink: 0; }
header h1 { font-size: 1rem; color: var(--bright); white-space: nowrap; }
.progress-bar { flex: 1; height: 6px; background: #2a2d3e; border-radius: 3px; overflow: hidden; }
.progress-fill { height: 100%; background: var(--pass); transition: width 0.4s ease; }
.stats { display: flex; gap: 14px; font-size: 0.82rem; white-space: nowrap; }
.stat.pass { color: var(--pass); } .stat.fail { color: var(--fail); }
.stat.skip { color: var(--skip); } .stat.pend { color: var(--text); }

main { display: flex; flex: 1; overflow: hidden; }

.sidebar { width: 270px; background: var(--surface); overflow-y: auto; border-right: 1px solid #2a2d3e; flex-shrink: 0; }
.sec-label { padding: 10px 14px 4px; font-size: 0.68rem; text-transform: uppercase; letter-spacing: 0.08em; color: var(--skip); border-top: 1px solid #2a2d3e; }
.sec-label:first-child { border-top: none; }
.step-row { display: flex; align-items: flex-start; gap: 8px; padding: 7px 14px; cursor: pointer; border-left: 3px solid transparent; transition: background 0.1s; font-size: 0.8rem; line-height: 1.35; }
.step-row:hover { background: rgba(255,255,255,0.04); }
.step-row.active { background: rgba(224,108,117,0.1); border-left-color: var(--accent); }
.step-row .sid { color: var(--skip); font-size: 0.72rem; min-width: 26px; padding-top: 1px; }
.step-row .stxt { flex: 1; color: var(--text); }
.step-row.active .stxt { color: var(--bright); }
.icon { font-size: 0.85rem; padding-top: 1px; }
.icon.pass { color: var(--pass); } .icon.fail { color: var(--fail); }
.icon.skip { color: var(--skip); } .icon.pending { color: #3a3f55; }

.content { flex: 1; display: flex; flex-direction: column; overflow: hidden; padding: 18px; gap: 14px; }

.cur-card { background: var(--surface); border: 1px solid var(--card); border-radius: 8px; padding: 18px; flex-shrink: 0; }
.cur-card .sec-name { font-size: 0.7rem; text-transform: uppercase; letter-spacing: 0.06em; color: var(--skip); margin-bottom: 6px; }
.cur-card .step-id  { font-size: 0.85rem; color: var(--accent); font-weight: 600; margin-bottom: 4px; display: flex; align-items: center; gap: 8px; }
.badge { font-size: 0.65rem; font-weight: 700; letter-spacing: 0.06em; padding: 2px 7px; border-radius: 3px; text-transform: uppercase; }
.badge.auto   { background: rgba(86,182,194,0.18); color: var(--cyan); border: 1px solid rgba(86,182,194,0.4); }
.badge.manual { background: rgba(229,192,123,0.15); color: var(--warn); border: 1px solid rgba(229,192,123,0.35); }
.cur-card h2 { font-size: 1.05rem; color: var(--bright); line-height: 1.4; margin-bottom: 12px; }
.expected { background: rgba(152,195,121,0.08); border: 1px solid rgba(152,195,121,0.25); border-radius: 6px; padding: 9px 13px; }
.expected .lbl { font-size: 0.68rem; text-transform: uppercase; letter-spacing: 0.06em; color: var(--pass); margin-bottom: 3px; }
.expected p { font-size: 0.88rem; }
.detected { margin-top: 10px; }
.detected .lbl { font-size: 0.68rem; text-transform: uppercase; letter-spacing: 0.06em; color: var(--cyan); margin-bottom: 4px; }
.det-line { font-family: 'Cascadia Code', 'Fira Code', monospace; font-size: 0.74rem; color: var(--cyan); background: rgba(0,0,0,0.25); padding: 3px 8px; border-radius: 4px; margin-top: 3px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

.actions { display: flex; gap: 10px; flex-shrink: 0; }
.btn { display: flex; align-items: center; gap: 7px; padding: 8px 16px; border-radius: 6px; border: 1px solid; cursor: pointer; font-size: 0.85rem; background: transparent; color: var(--text); transition: background 0.15s; }
.btn kbd { background: #2a2d3e; border: 1px solid #3a3f55; border-radius: 3px; padding: 1px 6px; font-size: 0.76rem; font-family: inherit; color: var(--text); }
.btn.pass { border-color: var(--pass); } .btn.pass:hover { background: rgba(152,195,121,0.12); color: var(--pass); }
.btn.fail { border-color: var(--fail); } .btn.fail:hover { background: rgba(224,108,117,0.12); color: var(--fail); }
.btn.skip { border-color: var(--skip); } .btn.skip:hover { background: rgba(122,132,153,0.12); }
.btn.nav  { border-color: #3a3f55; }     .btn.nav:hover  { background: rgba(255,255,255,0.05); }

.log-wrap { flex: 1; display: flex; flex-direction: column; overflow: hidden; min-height: 0; }
.log-wrap .lbl { font-size: 0.68rem; text-transform: uppercase; letter-spacing: 0.06em; color: var(--skip); margin-bottom: 6px; flex-shrink: 0; }
.log-box { flex: 1; overflow-y: auto; font-family: 'Cascadia Code', 'Fira Code', monospace; font-size: 0.74rem; background: #0d0f16; border-radius: 6px; padding: 10px; }
.log-line { padding: 1px 0; color: var(--cyan); border-bottom: 1px solid #151820; }
.log-line.new { animation: flash 0.6s ease-out; }
.no-log { color: var(--skip); font-style: italic; }
@keyframes flash { from { background: rgba(224,108,117,0.2); } to { background: transparent; } }
</style>
</head>
<body>
<header>
  <h1 id="plan-title">Test Runner</h1>
  <div class="progress-bar"><div class="progress-fill" id="prog"></div></div>
  <div class="stats">
    <span class="stat pass" id="s-pass">✓ 0</span>
    <span class="stat fail" id="s-fail">✗ 0</span>
    <span class="stat skip" id="s-skip">— 0</span>
    <span class="stat pend" id="s-pend">○ 0</span>
  </div>
</header>
<main>
  <div class="sidebar" id="sidebar"></div>
  <div class="content">
    <div class="cur-card">
      <div class="sec-name" id="c-sec"></div>
      <div class="step-id"><span id="c-id"></span><span id="c-badge"></span></div>
      <h2 id="c-action"></h2>
      <div class="expected"><div class="lbl">Expected</div><p id="c-expected"></p></div>
      <div class="detected" id="c-detected" style="display:none">
        <div class="lbl">Auto-detected log matches</div>
        <div id="c-det-lines"></div>
      </div>
    </div>
    <div class="actions">
      <button class="btn pass" onclick="setResult('pass')"><kbd>P</kbd> Pass</button>
      <button class="btn fail" onclick="setResult('fail')"><kbd>F</kbd> Fail</button>
      <button class="btn skip" onclick="setResult('skip')"><kbd>S</kbd> Skip</button>
      <button class="btn nav"  onclick="navDelta(-1)"><kbd>←</kbd> Prev</button>
      <button class="btn nav"  onclick="navDelta(1)"><kbd>→</kbd> Next</button>
    </div>
    <div class="log-wrap">
      <div class="lbl">Live log</div>
      <div class="log-box" id="log-box"><span class="no-log">Waiting for logs…</span></div>
    </div>
  </div>
</main>
<script>
let state = null;
const ICONS = { pass: '✓', fail: '✗', skip: '—', pending: '○' };

function esc(s) {
  return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function render(s) {
  state = s;
  const cur = s.steps[s.cur_idx];

  document.getElementById('plan-title').textContent = s.plan_name;
  document.title = s.plan_name + ' — Test Runner';

  const done = s.summary.passed + s.summary.failed + s.summary.skipped;
  document.getElementById('prog').style.width = `${done / s.summary.total * 100}%`;
  document.getElementById('s-pass').textContent = `✓ ${s.summary.passed}`;
  document.getElementById('s-fail').textContent = `✗ ${s.summary.failed}`;
  document.getElementById('s-skip').textContent = `— ${s.summary.skipped}`;
  document.getElementById('s-pend').textContent = `○ ${s.summary.pending}`;

  document.getElementById('c-sec').textContent      = cur.section;
  document.getElementById('c-id').textContent       = `Step ${cur.id}`;
  const badge = document.getElementById('c-badge');
  badge.className   = `badge ${cur.auto_pass ? 'auto' : 'manual'}`;
  badge.textContent = cur.auto_pass ? 'AUTO' : 'MANUAL';
  document.getElementById('c-action').textContent   = cur.action;
  document.getElementById('c-expected').textContent = cur.expected;

  const detWrap  = document.getElementById('c-detected');
  const detLines = document.getElementById('c-det-lines');
  if (cur.detected_logs.length > 0) {
    detWrap.style.display = 'block';
    detLines.innerHTML = cur.detected_logs
      .map(l => `<div class="det-line" title="${esc(l)}">${esc(l)}</div>`).join('');
  } else {
    detWrap.style.display = 'none';
  }

  let html = '', lastSec = null;
  s.steps.forEach((st, i) => {
    if (st.section !== lastSec) {
      html += `<div class="sec-label">${esc(st.section)}</div>`;
      lastSec = st.section;
    }
    const active = i === s.cur_idx ? ' active' : '';
    html += `<div class="step-row${active}" onclick="jumpTo('${st.id}')">
      <span class="sid">${st.id}</span>
      <span class="stxt">${esc(st.action)}</span>
      <span class="icon ${st.status}">${ICONS[st.status]}</span>
    </div>`;
  });
  document.getElementById('sidebar').innerHTML = html;
  document.querySelector('.step-row.active')?.scrollIntoView({ block: 'nearest' });
}

function appendLogs(lines) {
  const box   = document.getElementById('log-box');
  const noLog = box.querySelector('.no-log');
  if (noLog) box.innerHTML = '';
  const atBottom = box.scrollHeight - box.clientHeight <= box.scrollTop + 4;
  lines.forEach(line => {
    const d = document.createElement('div');
    d.className   = 'log-line new';
    d.textContent = line;
    box.appendChild(d);
  });
  while (box.children.length > 300) box.removeChild(box.firstChild);
  if (atBottom) box.scrollTop = box.scrollHeight;
}

function setResult(r) {
  if (!state) return;
  fetch(`/steps/${state.steps[state.cur_idx].id}/${r}`, { method: 'POST' });
}

function navDelta(d) {
  if (!state) return;
  const ni = state.cur_idx + d;
  if (ni >= 0 && ni < state.steps.length)
    fetch(`/navigate/${state.steps[ni].id}`, { method: 'POST' });
}

function jumpTo(id) { fetch(`/navigate/${id}`, { method: 'POST' }); }

document.addEventListener('keydown', e => {
  if (['INPUT','TEXTAREA'].includes(e.target.tagName)) return;
  if (e.key === 'p' || e.key === 'P') setResult('pass');
  if (e.key === 'f' || e.key === 'F') setResult('fail');
  if (e.key === 's' || e.key === 'S') setResult('skip');
  if (e.key === 'ArrowLeft')  navDelta(-1);
  if (e.key === 'ArrowRight') navDelta(1);
});

function connect() {
  const es = new EventSource('/stream');
  es.addEventListener('state', e => render(JSON.parse(e.data)));
  es.addEventListener('logs',  e => {
    appendLogs(JSON.parse(e.data).lines);
    fetch('/state').then(r => r.json()).then(render);
  });
  es.onerror = () => { es.close(); setTimeout(connect, 3000); };
}
connect();
</script>
</body>
</html>"""


# ---------------------------------------------------------------------------
# Entrypoint
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    import uvicorn

    args = _parse_args()
    plan = _load_plan(args.plan)

    _plan_name    = getattr(plan, "PLAN_NAME", args.plan)
    _log_prefixes = getattr(plan, "LOG_PREFIXES", DEFAULT_LOG_PREFIXES)
    _steps        = _build_steps(plan.STEPS)

    print(f"Test Runner — plan: {_plan_name} ({len(_steps)} steps)")
    print(f"Open: http://localhost:{args.port}")
    uvicorn.run(app, host="0.0.0.0", port=args.port, log_level="warning")
