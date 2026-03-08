#!/usr/bin/env bash
# Start the test runner and (optionally) launch the game via Steam.
#
# Usage: ./start-testing.sh --plan <name> [--game <steam-app-id>] [--no-browser]
#
# Examples:
#   ./start-testing.sh --plan cps --game 261550   # CPS tests + launch Bannerlord
#   ./start-testing.sh --plan cps                  # tests only, no game launch
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUNNER_PORT=8765
RUNNER_URL="http://localhost:${RUNNER_PORT}"

PLAN=""
GAME_APP_ID=""
OPEN_BROWSER=1

while [[ $# -gt 0 ]]; do
    case "$1" in
        --plan)       PLAN="$2";         shift 2 ;;
        --game)       GAME_APP_ID="$2";  shift 2 ;;
        --no-browser) OPEN_BROWSER=0;    shift ;;
        *) echo "Unknown argument: $1"; exit 1 ;;
    esac
done

if [[ -z "$PLAN" ]]; then
    # Auto-select if exactly one plan exists
    PLANS=($(ls "$SCRIPT_DIR/tests/plans/"*.py 2>/dev/null | xargs -I{} basename {} .py | grep -v '^_'))
    if [[ ${#PLANS[@]} -eq 1 ]]; then
        PLAN="${PLANS[0]}"
    else
        echo "Usage: $0 --plan <name> [--game <steam-app-id>] [--no-browser]"
        echo "Available plans: ${PLANS[*]:-none}"
        exit 1
    fi
fi

RUNNER_CMD="uv run --project GameLogMCP python tests/runner.py --plan $PLAN --port $RUNNER_PORT"

# ── Check if runner is already up ─────────────────────────────────────────────
if curl -sf "${RUNNER_URL}/state" >/dev/null 2>&1; then
    echo "Test runner already running at $RUNNER_URL"
else
    # ── Find a terminal emulator ───────────────────────────────────────────────
    TERM_CMD=""
    for term in kitty alacritty foot wezterm ghostty gnome-terminal; do
        if command -v "$term" &>/dev/null; then
            case "$term" in
                kitty)         TERM_CMD="kitty --title 'Test Runner' -- bash -c" ;;
                alacritty)     TERM_CMD="alacritty --title 'Test Runner' -e bash -c" ;;
                foot)          TERM_CMD="foot --title 'Test Runner' bash -c" ;;
                wezterm)       TERM_CMD="wezterm start --cwd '$SCRIPT_DIR' -- bash -c" ;;
                ghostty)       TERM_CMD="ghostty --title='Test Runner' -e bash -c" ;;
                gnome-terminal) TERM_CMD="gnome-terminal --title='Test Runner' -- bash -c" ;;
            esac
            break
        fi
    done

    INNER="cd '$SCRIPT_DIR' && $RUNNER_CMD; echo; echo 'Runner stopped — press Enter to close.'; read"

    if [[ -n "$TERM_CMD" ]]; then
        eval "$TERM_CMD '$INNER'" &
    else
        echo "No terminal emulator found — running in background (log: /tmp/test-runner.log)"
        (cd "$SCRIPT_DIR" && eval "$RUNNER_CMD") > /tmp/test-runner.log 2>&1 &
    fi

    # ── Wait for runner ────────────────────────────────────────────────────────
    echo -n "Waiting for test runner"
    for i in $(seq 1 15); do
        sleep 1; echo -n "."
        if curl -sf "${RUNNER_URL}/state" >/dev/null 2>&1; then
            echo " ready."; break
        fi
        [[ $i -eq 15 ]] && echo " timed out — check the runner window for errors."
    done
fi

# ── Open browser ───────────────────────────────────────────────────────────────
if [[ $OPEN_BROWSER -eq 1 ]]; then
    xdg-open "$RUNNER_URL" &
fi

# ── Launch game ────────────────────────────────────────────────────────────────
if [[ -n "$GAME_APP_ID" ]]; then
    echo "Launching game via Steam (app $GAME_APP_ID)..."
    steam "steam://rungameid/$GAME_APP_ID" &
fi

echo "Done. Test runner: $RUNNER_URL  (plan: $PLAN)"
