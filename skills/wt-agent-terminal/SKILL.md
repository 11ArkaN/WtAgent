---
name: wt-agent-terminal
description: Operate the local wt-agent CLI to drive Windows Terminal with real screenshots. Use when Codex needs to run a command in a visible terminal, keep a recoverable interactive session, enter WSL inside the same terminal session, inspect or recover lost sessions, interrupt long-running work, or verify terminal rendering from screenshots instead of plain text alone.
---

# Wt Agent Terminal

Use `wt-agent` as the primary interface for Windows Terminal work that must preserve real screenshots.

This skill is stored in a `skills.sh`-compatible `skills/<name>` layout, so it can be installed into multiple agent ecosystems such as Codex, Claude Code, Cursor, and other tools supported by `npx skills add`.

If `wt-agent` is missing, prefer the normal Windows installer from the latest GitHub Release: `WtAgent-Setup.exe`. Use the PowerShell scripts in `scripts/` only for local development or unpublished branches.

## Quick Start

- Prefer `wt-agent run` for one-shot commands where a single end-state screenshot is enough.
- Prefer `wt-agent session-start` for multi-step work, interactive commands, WSL, inspection, interruption, or recovery.
- Treat the JSON result as the control plane. Read `status`, `sessionId`, `artifacts`, and `live`.
- Treat the screenshot as the source of truth for visual correctness. Open `latestCapturePath` when judging what the terminal actually showed.
- Use `session-list` and `session-inspect` whenever the current context loses the session id.
- Expect global `wt-agent` to work from any directory once the installer has added the launcher to the user `PATH`.
- Prefer `npx skills add . --skill wt-agent-terminal` or `npx skills add <repo-url> --skill wt-agent-terminal` when installing this skill into non-Codex agents.

## Core Workflow

1. Start a session with `wt-agent session-start --profile "Windows PowerShell" --cwd "<path>"`.
2. Read the returned `sessionId`.
3. Send text with `wt-agent session-send --session-id <id> --input "<command>"`.
4. For staged typing, pass `--submit false`, then finalize with `wt-agent session-submit --session-id <id>`.
5. Poll with `wt-agent session-status --session-id <id>` if the command may take time.
6. Capture a fresh real screenshot with `wt-agent session-capture --session-id <id>` whenever the latest image may be stale.
7. Interrupt with `wt-agent session-interrupt --session-id <id>` for `Ctrl+C`.
8. Stop with `wt-agent session-stop --session-id <id>` when the session is no longer needed.

## WSL Workflow

- Prefer `wt-agent session-enter-wsl --session-id <id>` over manually sending plain `wsl` when you want tracked nested-shell state.
- After entering WSL, expect `live.shellKind` to become `wsl-bash` and `live.stateSource` to become `nested`.
- Continue to use the same `session-send`, `session-status`, `session-capture`, `session-interrupt`, and `session-stop` commands after entering WSL.
- Use `wait-mode prompt` when you want the command to block until the next WSL prompt is observed.

## Recovery Rules

- If the agent loses track of a session id, run `wt-agent session-list` first.
- Use `wt-agent session-inspect --session-id <id>` to rebuild full state for a specific session.
- Prefer recovery over opening a new session when the old terminal still contains useful context.
- If a session looks alive in metadata but the terminal is gone, treat it as stale and start a fresh session.

## Verification Rules

- Open the latest capture before concluding that the terminal output looks correct.
- Prefer screenshot evidence over transcript formatting when the task is about what was visibly rendered.
- Remember that stitched images are composed from real screenshots; verify the final image itself for missing lines or duplicates.
- Use `transcriptTail` for quick state checks, not as a replacement for visual inspection.

## Reference

Read [references/commands.md](references/commands.md) for concrete command patterns.
