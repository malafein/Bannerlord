#!/usr/bin/env python3
"""
CPS Interactive Test Runner
Guides through the Calradian Postal Service manual test plan with live log monitoring.

Setup:  pip install -r tests/requirements.txt
Run:    python tests/runner.py
Open:   http://localhost:8765
"""

import asyncio
import glob
import json
import os
import re
import time
from contextlib import asynccontextmanager
from dataclasses import dataclass, field
from enum import Enum
from typing import Optional

from fastapi import FastAPI, Request
from fastapi.responses import HTMLResponse, JSONResponse, StreamingResponse

# ---------------------------------------------------------------------------
# Log reading (mirrors GameLogMCP)
# ---------------------------------------------------------------------------

LOG_DIR = os.path.join(os.path.dirname(__file__), "..", "GameLogs")
MOD_PREFIXES = ("[CalradianPostalService]", "[BloodlineProgression]")
MANGOHUD_FILE = os.path.join(os.path.dirname(__file__), "mangohud_status.txt")


def _latest_log() -> Optional[str]:
    pattern = os.path.join(LOG_DIR, "rgl_log_*.txt")
    files = glob.glob(pattern)
    return max(files, key=os.path.getmtime) if files else None


def _read_mod_lines_since(path: str, pos: int) -> tuple[list[str], int]:
    with open(path, encoding="utf-8", errors="replace") as f:
        f.seek(pos)
        lines = [line.rstrip() for line in f if any(p in line for p in MOD_PREFIXES)]
        new_pos = f.tell()
    return lines, new_pos


# ---------------------------------------------------------------------------
# Test cases
# ---------------------------------------------------------------------------

class Status(str, Enum):
    PENDING = "pending"
    PASS    = "pass"
    FAIL    = "fail"
    SKIP    = "skip"


@dataclass
class Step:
    id:             str
    section:        str
    action:         str
    expected:       str
    status:         Status      = Status.PENDING
    detected_logs:  list[str]   = field(default_factory=list)
    # Regex patterns: if ANY match a new log line, flag it for this step
    auto_patterns:  list[str]   = field(default_factory=list)
    # If True, a pattern match auto-passes the step instead of just flagging
    auto_pass:      bool        = False


STEPS: list[Step] = [
    # -----------------------------------------------------------------------
    # 1. Personal Missives — Access & Cost
    # -----------------------------------------------------------------------
    Step("1.1", "1. Personal Missives — Access & Cost",
         "Enter a town → Find a courier → Send a personal letter",
         "Recipient list appears"),
    Step("1.2", "1. Personal Missives — Access & Cost",
         "Select a recipient",
         "Menu shows fee of 50g, not the distance-based diplomatic fee"),
    Step("1.3", "1. Personal Missives — Access & Cost",
         "With < 50g gold, attempt to send",
         'Option is disabled with "not enough gold" tooltip'),
    Step("1.4", "1. Personal Missives — Access & Cost",
         "Send a friendly missive",
         '50g deducted; "Missive sent" log entry appears',
         auto_patterns=[r"\[MissiveFriendly\] Missive sent"]),

    # -----------------------------------------------------------------------
    # 2. Personal Missives — Cooldown
    # -----------------------------------------------------------------------
    Step("2.1", "2. Personal Missives — Cooldown",
         "Send any personal missive to Recipient A",
         "Succeeds; cooldown set log appears",
         auto_patterns=[r"\[Cooldown\].*cooldown set"]),
    Step("2.2", "2. Personal Missives — Cooldown",
         "Immediately try to send another to Recipient A",
         'Option disabled with "Wait X days" tooltip — verify visually'),
    Step("2.3", "2. Personal Missives — Cooldown",
         "Send a personal missive to Recipient B (different hero)",
         "Still allowed — cooldown is per-recipient",
         auto_patterns=[r"\[Missive(?:Friendly|Threat)\] Missive sent"]),
    Step("2.4", "2. Personal Missives — Cooldown",
         "Save, reload, check Recipient A option",
         'Still disabled (cooldown persisted); "Loaded N missives, N cooldowns" in log',
         auto_patterns=[r"Loaded \d+ missives, \d+ cooldowns"]),
    Step("2.5", "2. Personal Missives — Cooldown",
         "Advance time 7+ days, check Recipient A option",
         "Now enabled again — verify visually"),

    # -----------------------------------------------------------------------
    # 3. Friendly Missive — Outcomes
    # -----------------------------------------------------------------------
    Step("3.1", "3. Friendly Missive — Outcomes",
         "Send to a generous/earnest recipient",
         '"was moved by your letter" or "appreciated your letter" in log',
         auto_patterns=[r"was moved by your letter|appreciated your letter"]),
    Step("3.2", "3. Friendly Missive — Outcomes",
         "Send to a curt recipient with low relation",
         '"was not pleased by your letter" possible in log',
         auto_patterns=[r"was not pleased by your letter|received your letter"]),
    Step("3.3", "3. Friendly Missive — Outcomes",
         "Send to a close friend (relation ~80+)",
         "Reception likely, but relation gain unlikely (diminishing returns) — check improvementChance in log",
         auto_patterns=[r"\[MissiveFriendly\] APPRECIATED improvementChance"]),
    Step("3.4", "3. Friendly Missive — Outcomes",
         "Send to an enemy (relation ~-80)",
         "Reception unlikely; if backfire fires, −1 to −3 relation in log",
         auto_patterns=[r"\[MissiveFriendly\] NOT APPRECIATED backfireChance"]),
    Step("3.5", "3. Friendly Missive — Outcomes",
         "Check debug log",
         "Verify receptionChance, improvementChance/backfireChance, and rolls all printed",
         auto_patterns=[r"\[MissiveFriendly\].*receptionChance"]),

    # -----------------------------------------------------------------------
    # 4. Threatening Missive — Outcomes
    # -----------------------------------------------------------------------
    Step("4.1", "4. Threatening Missive — Outcomes",
         "Send to a high-valor recipient (valor ≥ 2)",
         '"chosen to defy you" in log; no relation change',
         auto_patterns=[r"chosen to defy you"]),
    Step("4.2", "4. Threatening Missive — Outcomes",
         "Send to a high-ironic recipient",
         '"found your threat more amusing" in log; no relation change',
         auto_patterns=[r"found your threat more amusing"]),
    Step("4.3", "4. Threatening Missive — Outcomes",
         "Send to an honorable recipient with warm relations",
         '"was angered by your letter" with −2 or −3 penalty in log',
         auto_patterns=[r"was angered by your letter"]),
    Step("4.4", "4. Threatening Missive — Outcomes",
         "Send to a low-relation, low-honor recipient",
         '"was not impressed" in log',
         auto_patterns=[r"received your letter, but was not impressed"]),
    Step("4.5", "4. Threatening Missive — Outcomes",
         "Check debug log",
         "Three [MissiveThreat] debug lines: defianceChance/roll1a, amusementChance/roll1b, angerChance/roll2",
         auto_patterns=[r"\[MissiveThreat\].*roll1a", r"\[MissiveThreat\].*roll1b", r"\[MissiveThreat\].*roll2"]),

    # -----------------------------------------------------------------------
    # 5. Peace Missive — Target Selection
    # -----------------------------------------------------------------------
    Step("5.1", "5. Peace Missive — Target Selection",
         "Select a recipient at war with multiple factions",
         "Peace option enabled; target selection lists all warring factions"),
    Step("5.2", "5. Peace Missive — Target Selection",
         "Select a recipient not at war with anyone",
         'Peace option disabled with "not currently at war" tooltip'),
    Step("5.3", "5. Peace Missive — Target Selection",
         "Select a third-party faction as the peace target",
         "Missive sent; [MissivePeace] debug log shows target faction name",
         auto_patterns=[r"\[MissivePeace\] Missive sent"]),
    Step("5.4", "5. Peace Missive — Target Selection",
         "Select your own faction as the peace target",
         "Works as before; [MissivePeace] debug log shows target:YourFaction",
         auto_patterns=[r"\[MissivePeace\] Missive sent"]),

    # -----------------------------------------------------------------------
    # 6. Diplomatic Missives — Save/Load
    # -----------------------------------------------------------------------
    Step("6.1", "6. Diplomatic Missives — Save/Load",
         "Send a peace/war/join-war/alliance missive",
         '"Missive sent, arrives in X days" in log',
         auto_patterns=[r"Missive sent to .+\. Arrives in"]),
    Step("6.2", "6. Diplomatic Missives — Save/Load",
         "Save the game before arrival",
         '"Saved N missives, N cooldowns" in log',
         auto_patterns=[r"Saved \d+ missives, \d+ cooldowns"]),
    Step("6.3", "6. Diplomatic Missives — Save/Load",
         "Load the save",
         '"Loaded N missives, N cooldowns" — count matches what was sent',
         auto_patterns=[r"Loaded \d+ missives, \d+ cooldowns"]),
    Step("6.4", "6. Diplomatic Missives — Save/Load",
         "Advance time to delivery",
         '"Missive delivered" in log; outcome logged normally',
         auto_patterns=[r"Missive delivered from"]),

    # -----------------------------------------------------------------------
    # 7. Charm XP
    # -----------------------------------------------------------------------
    Step("7.1", "7. Charm XP",
         "Note Charm XP before sending (open character screen → Skills → Social)",
         "Baseline recorded — no log to detect; mark manually"),
    Step("7.2", "7. Charm XP",
         "Send a friendly missive; recipient appreciates it",
         '+15 Charm XP logged: "[CharmXP] +15 Charm XP granted"',
         auto_patterns=[r"\[CharmXP\] \+15 Charm XP"]),
    Step("7.3", "7. Charm XP",
         "Send a threatening missive that angers the recipient",
         '+15 Charm XP logged: "[CharmXP] +15 Charm XP granted"',
         auto_patterns=[r"\[CharmXP\] \+15 Charm XP"]),
    Step("7.4", "7. Charm XP",
         "Send any diplomatic missive (peace/war/join-war/alliance)",
         '+20 Charm XP logged: "[CharmXP] +20 Charm XP granted"',
         auto_patterns=[r"\[CharmXP\] \+20 Charm XP"]),
    Step("7.5", "7. Charm XP",
         "Send a friendly missive that is ignored or backfires (check log carefully)",
         "No [CharmXP] line — XP is only granted when reception succeeds",
         auto_patterns=[r"\[MissiveFriendly\] NOT APPRECIATED"]),
]


# ---------------------------------------------------------------------------
# App state
# ---------------------------------------------------------------------------

_state: dict = {
    "steps":        STEPS,
    "current_idx":  0,
    "log_tail":     [],   # all recent mod log lines
    "log_path":     None,
    "log_pos":      0,
}

_sse_clients: list[asyncio.Queue] = []


def _snapshot() -> dict:
    s = _state
    summary = {
        "total":   len(s["steps"]),
        "passed":  sum(1 for st in s["steps"] if st.status == Status.PASS),
        "failed":  sum(1 for st in s["steps"] if st.status == Status.FAIL),
        "skipped": sum(1 for st in s["steps"] if st.status == Status.SKIP),
        "pending": sum(1 for st in s["steps"] if st.status == Status.PENDING),
    }
    return {
        "steps": [
            {
                "id":            st.id,
                "section":       st.section,
                "action":        st.action,
                "expected":      st.expected,
                "status":        st.status,
                "detected_logs": st.detected_logs[-6:],
            }
            for st in s["steps"]
        ],
        "current_idx": s["current_idx"],
        "log_tail":    s["log_tail"][-30:],
        "summary":     summary,
    }


async def _broadcast(event: str, data: dict) -> None:
    msg = f"event: {event}\ndata: {json.dumps(data)}\n\n"
    for q in list(_sse_clients):
        await q.put(msg)


def _write_mangohud() -> None:
    """Write current step summary for optional MangoHUD file_read overlay."""
    try:
        s = _state
        cur = s["steps"][s["current_idx"]]
        total = len(s["steps"])
        passed  = sum(1 for st in s["steps"] if st.status == Status.PASS)
        failed  = sum(1 for st in s["steps"] if st.status == Status.FAIL)
        skipped = sum(1 for st in s["steps"] if st.status == Status.SKIP)
        text = (
            f"CPS Test Runner\n"
            f"{cur.section}\n"
            f"Step {cur.id}/{total}: {cur.action[:40]}\n"
            f"Pass:{passed}  Fail:{failed}  Skip:{skipped}"
        )
        with open(MANGOHUD_FILE, "w") as f:
            f.write(text)
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

            lines, new_pos = _read_mod_lines_since(path, _state["log_pos"])
            _state["log_pos"] = new_pos

            if not lines:
                continue

            _state["log_tail"].extend(lines)
            _state["log_tail"] = _state["log_tail"][-200:]

            cur = _state["steps"][_state["current_idx"]]
            state_changed = False

            for line in lines:
                for pattern in cur.auto_patterns:
                    if re.search(pattern, line, re.IGNORECASE):
                        if line not in cur.detected_logs:
                            cur.detected_logs.append(line)
                            state_changed = True
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
    task = asyncio.create_task(_log_poll_loop())
    _write_mangohud()
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
    for i, step in enumerate(_state["steps"]):
        if step.id == step_id:
            step.status = Status(result)
            # Advance current_idx to next pending step
            for j in range(i + 1, len(_state["steps"])):
                if _state["steps"][j].status == Status.PENDING:
                    _state["current_idx"] = j
                    break
            await _broadcast("state", _snapshot())
            _write_mangohud()
            return JSONResponse({"ok": True})
    return JSONResponse({"error": "not found"}, status_code=404)


@app.post("/navigate/{step_id}")
async def navigate(step_id: str):
    for i, step in enumerate(_state["steps"]):
        if step.id == step_id:
            _state["current_idx"] = i
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
<title>CPS Test Runner</title>
<style>
:root {
  --bg:      #12141c;
  --surface: #1a1d2e;
  --card:    #232640;
  --accent:  #e06c75;
  --pass:    #98c379;
  --fail:    #e06c75;
  --skip:    #7a8499;
  --warn:    #e5c07b;
  --cyan:    #56b6c2;
  --text:    #abb2bf;
  --bright:  #dde1e7;
}
* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: 'Segoe UI', system-ui, sans-serif; background: var(--bg); color: var(--text); height: 100vh; display: flex; flex-direction: column; overflow: hidden; }

/* ── Header ── */
header { background: var(--surface); padding: 10px 18px; display: flex; align-items: center; gap: 14px; border-bottom: 1px solid #2a2d3e; flex-shrink: 0; }
header h1 { font-size: 1rem; color: var(--bright); white-space: nowrap; }
.progress-bar { flex: 1; height: 6px; background: #2a2d3e; border-radius: 3px; overflow: hidden; }
.progress-fill { height: 100%; background: var(--pass); transition: width 0.4s ease; }
.stats { display: flex; gap: 14px; font-size: 0.82rem; white-space: nowrap; }
.stat.pass { color: var(--pass); }
.stat.fail { color: var(--fail); }
.stat.skip { color: var(--skip); }
.stat.pend { color: var(--text); }

/* ── Layout ── */
main { display: flex; flex: 1; overflow: hidden; }

/* ── Sidebar ── */
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
.icon.pass { color: var(--pass); }
.icon.fail { color: var(--fail); }
.icon.skip { color: var(--skip); }
.icon.pending { color: #3a3f55; }

/* ── Content ── */
.content { flex: 1; display: flex; flex-direction: column; overflow: hidden; padding: 18px; gap: 14px; }

/* ── Current step card ── */
.cur-card { background: var(--surface); border: 1px solid var(--card); border-radius: 8px; padding: 18px; flex-shrink: 0; }
.cur-card .sec-name { font-size: 0.7rem; text-transform: uppercase; letter-spacing: 0.06em; color: var(--skip); margin-bottom: 6px; }
.cur-card .step-id  { font-size: 0.85rem; color: var(--accent); font-weight: 600; margin-bottom: 4px; }
.cur-card h2 { font-size: 1.05rem; color: var(--bright); line-height: 1.4; margin-bottom: 12px; }
.expected { background: rgba(152,195,121,0.08); border: 1px solid rgba(152,195,121,0.25); border-radius: 6px; padding: 9px 13px; }
.expected .lbl { font-size: 0.68rem; text-transform: uppercase; letter-spacing: 0.06em; color: var(--pass); margin-bottom: 3px; }
.expected p { font-size: 0.88rem; color: var(--text); }
.detected { margin-top: 10px; }
.detected .lbl { font-size: 0.68rem; text-transform: uppercase; letter-spacing: 0.06em; color: var(--cyan); margin-bottom: 4px; }
.det-line { font-family: 'Cascadia Code', 'Fira Code', monospace; font-size: 0.74rem; color: var(--cyan); background: rgba(0,0,0,0.25); padding: 3px 8px; border-radius: 4px; margin-top: 3px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

/* ── Action buttons ── */
.actions { display: flex; gap: 10px; flex-shrink: 0; }
.btn { display: flex; align-items: center; gap: 7px; padding: 8px 16px; border-radius: 6px; border: 1px solid; cursor: pointer; font-size: 0.85rem; background: transparent; color: var(--text); transition: background 0.15s; }
.btn kbd { background: #2a2d3e; border: 1px solid #3a3f55; border-radius: 3px; padding: 1px 6px; font-size: 0.76rem; font-family: inherit; color: var(--text); }
.btn.pass { border-color: var(--pass); } .btn.pass:hover { background: rgba(152,195,121,0.12); color: var(--pass); }
.btn.fail { border-color: var(--fail); } .btn.fail:hover { background: rgba(224,108,117,0.12); color: var(--fail); }
.btn.skip { border-color: var(--skip); } .btn.skip:hover { background: rgba(122,132,153,0.12); }
.btn.nav  { border-color: #3a3f55; }     .btn.nav:hover  { background: rgba(255,255,255,0.05); }

/* ── Log tail ── */
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
  <h1>⚔ CPS Test Runner</h1>
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
      <div class="step-id"  id="c-id"></div>
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
      <div class="lbl">Live mod log</div>
      <div class="log-box" id="log-box"><span class="no-log">Waiting for game logs…</span></div>
    </div>
  </div>
</main>
<script>
let state = null;

function render(s) {
  state = s;
  const cur = s.steps[s.current_idx];

  // Header
  const done = s.summary.passed + s.summary.failed + s.summary.skipped;
  document.getElementById('prog').style.width = `${done / s.summary.total * 100}%`;
  document.getElementById('s-pass').textContent = `✓ ${s.summary.passed}`;
  document.getElementById('s-fail').textContent = `✗ ${s.summary.failed}`;
  document.getElementById('s-skip').textContent = `— ${s.summary.skipped}`;
  document.getElementById('s-pend').textContent = `○ ${s.summary.pending}`;

  // Current card
  document.getElementById('c-sec').textContent    = cur.section;
  document.getElementById('c-id').textContent     = `Step ${cur.id}`;
  document.getElementById('c-action').textContent = cur.action;
  document.getElementById('c-expected').textContent = cur.expected;

  const detWrap = document.getElementById('c-detected');
  const detLines = document.getElementById('c-det-lines');
  if (cur.detected_logs.length > 0) {
    detWrap.style.display = 'block';
    detLines.innerHTML = cur.detected_logs
      .map(l => `<div class="det-line" title="${escHtml(l)}">${escHtml(l)}</div>`)
      .join('');
  } else {
    detWrap.style.display = 'none';
  }

  // Sidebar
  const ICONS = { pass: '✓', fail: '✗', skip: '—', pending: '○' };
  let html = '';
  let lastSec = null;
  s.steps.forEach((st, i) => {
    if (st.section !== lastSec) {
      html += `<div class="sec-label">${escHtml(st.section)}</div>`;
      lastSec = st.section;
    }
    const active = i === s.current_idx ? ' active' : '';
    html += `<div class="step-row${active}" onclick="jumpTo('${st.id}')">
      <span class="sid">${st.id}</span>
      <span class="stxt">${escHtml(st.action)}</span>
      <span class="icon ${st.status}">${ICONS[st.status]}</span>
    </div>`;
  });
  document.getElementById('sidebar').innerHTML = html;
  const activeEl = document.querySelector('.step-row.active');
  if (activeEl) activeEl.scrollIntoView({ block: 'nearest' });
}

function appendLogs(lines) {
  const box = document.getElementById('log-box');
  const noLog = box.querySelector('.no-log');
  if (noLog) box.innerHTML = '';
  const wasBottom = box.scrollHeight - box.clientHeight <= box.scrollTop + 4;
  lines.forEach(line => {
    const d = document.createElement('div');
    d.className = 'log-line new';
    d.textContent = line;
    box.appendChild(d);
  });
  while (box.children.length > 300) box.removeChild(box.firstChild);
  if (wasBottom) box.scrollTop = box.scrollHeight;
}

function escHtml(s) {
  return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function setResult(r) {
  if (!state) return;
  const cur = state.steps[state.current_idx];
  fetch(`/steps/${cur.id}/${r}`, { method: 'POST' });
}

function navDelta(d) {
  if (!state) return;
  const ni = state.current_idx + d;
  if (ni >= 0 && ni < state.steps.length)
    fetch(`/navigate/${state.steps[ni].id}`, { method: 'POST' });
}

function jumpTo(id) {
  fetch(`/navigate/${id}`, { method: 'POST' });
}

document.addEventListener('keydown', e => {
  if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
  if (e.key === 'p' || e.key === 'P') setResult('pass');
  if (e.key === 'f' || e.key === 'F') setResult('fail');
  if (e.key === 's' || e.key === 'S') setResult('skip');
  if (e.key === 'ArrowLeft')  navDelta(-1);
  if (e.key === 'ArrowRight') navDelta(1);
});

// SSE
function connect() {
  const es = new EventSource('/stream');
  es.addEventListener('state', e => render(JSON.parse(e.data)));
  es.addEventListener('logs',  e => {
    const d = JSON.parse(e.data);
    appendLogs(d.lines);
    // Re-render to pick up detected_logs updated by server
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
    print("CPS Test Runner → http://localhost:8765")
    uvicorn.run(app, host="0.0.0.0", port=8765, log_level="warning")
