"""
MCP server for Bannerlord mod log analysis.
Surfaces [CalradianPostalService] and [BloodlineProgression] entries
from the game's rgl_log files without needing to grep manually.
"""

import glob
import os
import re
from mcp.server.fastmcp import FastMCP

LOG_DIR = os.path.join(os.path.dirname(__file__), "..", "GameLogs")
MOD_PREFIXES = ("[CalradianPostalService]", "[BloodlineProgression]")
LOG_LINE_RE = re.compile(r"^\[(\d{2}:\d{2}:\d{2}\.\d+)\] (.+)$")


def _latest_log() -> str | None:
    pattern = os.path.join(LOG_DIR, "rgl_log_*.txt")
    files = glob.glob(pattern)
    return max(files, key=os.path.getmtime) if files else None


def _read_mod_lines(log_path: str) -> list[str]:
    with open(log_path, encoding="utf-8", errors="replace") as f:
        return [line.rstrip() for line in f if any(p in line for p in MOD_PREFIXES)]


mcp = FastMCP("bannerlord-logs")


@mcp.tool()
def get_mod_logs(max_entries: int = 100) -> str:
    """Return the most recent mod log entries from the latest rgl_log file."""
    path = _latest_log()
    if not path:
        return "No rgl_log files found."
    lines = _read_mod_lines(path)
    if not lines:
        return f"No mod log entries found in {os.path.basename(path)}."
    result = lines[-max_entries:]
    header = f"=== {os.path.basename(path)} — last {len(result)} mod entries ===\n"
    return header + "\n".join(result)


@mcp.tool()
def get_errors() -> str:
    """Return all ERROR log entries from both mods in the latest session."""
    path = _latest_log()
    if not path:
        return "No rgl_log files found."
    lines = _read_mod_lines(path)
    errors = [l for l in lines if "ERROR" in l]
    if not errors:
        return f"No errors found in {os.path.basename(path)}."
    return f"=== {os.path.basename(path)} — {len(errors)} error(s) ===\n" + "\n".join(errors)


@mcp.tool()
def get_missive_events() -> str:
    """Return missive send and delivery events from the latest session."""
    path = _latest_log()
    if not path:
        return "No rgl_log files found."
    lines = _read_mod_lines(path)
    keywords = ("Missive sent", "Missive delivered", "missive", "OnSend", "OnDelivery")
    events = [l for l in lines if any(k.lower() in l.lower() for k in keywords)]
    if not events:
        return f"No missive events found in {os.path.basename(path)}."
    return f"=== {os.path.basename(path)} — {len(events)} missive event(s) ===\n" + "\n".join(events)


@mcp.tool()
def get_session_summary() -> str:
    """Return a high-level summary of the latest game session for both mods."""
    path = _latest_log()
    if not path:
        return "No rgl_log files found."

    lines = _read_mod_lines(path)
    total = len(lines)
    errors = sum(1 for l in lines if "ERROR" in l)
    missives_sent = sum(1 for l in lines if "Missive sent" in l)
    missives_delivered = sum(1 for l in lines if "Missive delivered" in l)
    level_ups = sum(1 for l in lines if "LevelUp" in l)
    loaded = [l for l in lines if "loaded" in l.lower() and ("version" in l.lower() or "Mod loaded" in l)]

    summary = [
        f"=== Session summary: {os.path.basename(path)} ===",
        f"Total mod log entries : {total}",
        f"Errors                : {errors}",
        f"Missives sent         : {missives_sent}",
        f"Missives delivered    : {missives_delivered}",
        f"Level-up events       : {level_ups}",
    ]
    if loaded:
        summary.append("\nLoad events:")
        summary.extend(f"  {l}" for l in loaded)
    if errors > 0:
        summary.append("\nErrors:")
        summary.extend(f"  {l}" for l in lines if "ERROR" in l)

    return "\n".join(summary)


@mcp.tool()
def list_sessions() -> str:
    """List all available rgl_log files with their timestamps and sizes."""
    pattern = os.path.join(LOG_DIR, "rgl_log_*.txt")
    files = sorted(glob.glob(pattern), key=os.path.getmtime, reverse=True)
    if not files:
        return "No rgl_log files found."
    rows = []
    for f in files:
        stat = os.stat(f)
        mod_lines = _read_mod_lines(f)
        rows.append(
            f"{os.path.basename(f)}  "
            f"size={stat.st_size // 1024}KB  "
            f"mod_entries={len(mod_lines)}"
        )
    return "=== Available sessions (newest first) ===\n" + "\n".join(rows)


if __name__ == "__main__":
    mcp.run()
