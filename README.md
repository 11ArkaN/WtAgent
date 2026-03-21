# WtAgent

`WtAgent` is a Windows Terminal wrapper for AI agents.

It runs commands in a real Windows Terminal window, captures real screenshots of the rendered output, keeps recoverable interactive sessions, and can continue inside WSL while preserving visual evidence and machine-readable state.

## Why

Most terminal tooling gives an agent only plain text. `WtAgent` adds a second channel:

- real screenshots of what Windows Terminal actually rendered
- structured JSON for runs and interactive sessions
- recoverable session state after context loss
- interrupt support for long-running commands
- nested WSL tracking inside the same terminal session

## What It Does

- `run`: execute one command and return JSON plus a real screenshot
- `session-start`: open a long-lived Windows Terminal session
- `session-send`: type input into the live terminal
- `session-submit`: submit staged input later so the typed command is visible on-screen
- `session-capture`: take a fresh screenshot of the current terminal view
- `session-interrupt`: send `Ctrl+C`
- `session-list` and `session-inspect`: recover lost sessions
- `session-enter-wsl`: switch the live session into tracked WSL bash mode

## Flow

```mermaid
flowchart LR
    A["Agent"] --> B["wt-agent CLI"]
    B --> C["Windows Terminal"]
    C --> D["Real screenshot (.png)"]
    C --> E["Transcript/log files"]
    B --> F["JSON result/state"]
```

## Install

Default install path is GitHub Releases:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-wt-agent.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\install-wt-agent-skill.ps1
```

That downloads the latest `wt-agent-win-x64.zip` release asset, installs it into `%LOCALAPPDATA%\wt-agent\current`, and exposes `wt-agent` through `%USERPROFILE%\.local\bin`.

The second command installs the Codex skill from the same release into `%CODEX_HOME%\skills\wt-agent-terminal` or `%USERPROFILE%\.codex\skills\wt-agent-terminal`.

## Agent Skill

This repo is laid out in a `skills.sh`-compatible structure:

- [skills/wt-agent-terminal](/C:/Users/ArkaN/Downloads/sulima%20lab2/skills/wt-agent-terminal)

That means the skill can be installed to multiple agent ecosystems, not only Codex.

Install from a local checkout with `skills.sh`:

```bash
npx skills add . --skill wt-agent-terminal
```

Install from GitHub for authenticated users:

```bash
npx skills add https://github.com/11ArkaN/WtAgent --skill wt-agent-terminal
```

Target a specific agent explicitly:

```bash
npx skills add . --skill wt-agent-terminal --agent codex
npx skills add . --skill wt-agent-terminal --agent claude-code
npx skills add . --skill wt-agent-terminal --agent cursor
```

Use `scripts/install-wt-agent-skill.ps1` only as the convenience path for Codex-style local installs on Windows. For cross-agent installation, prefer `npx skills add ...`.

For unpublished local changes:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-wt-agent.ps1 -Source local
powershell -ExecutionPolicy Bypass -File .\scripts\install-wt-agent-skill.ps1 -Source local
```

## Quick Start

Run one command:

```powershell
wt-agent run --command "curl.exe -v https://httpbin.org/get" --profile "Windows PowerShell" --cwd "C:\work"
```

Start an interactive session:

```powershell
wt-agent session-start --profile "Windows PowerShell" --cwd "C:\work"
wt-agent session-send --session-id <id> --input "curl.exe -v https://httpbin.org/get"
wt-agent session-stop --session-id <id>
```

Enter WSL in the same session:

```powershell
wt-agent session-enter-wsl --session-id <id>
wt-agent session-send --session-id <id> --input "pwd"
```

Recover a lost session:

```powershell
wt-agent session-list
wt-agent session-inspect --session-id <id>
```

## Output Model

Every command returns JSON. Important fields:

- `status`: success or failure state
- `artifacts`: file paths for screenshots and logs
- `window`: terminal PID and HWND when available
- `live`: current state for interactive sessions
- `shellKind` and `stateSource`: whether the active shell is PowerShell or nested WSL

## Releases

- Repo: [11ArkaN/WtAgent](https://github.com/11ArkaN/WtAgent)
- Latest release assets are published from GitHub Actions on tags `v*`
- Current installer target asset name: `wt-agent-win-x64.zip`
- Current skill asset name: `wt-agent-terminal-skill.zip`

## Development

Build:

```powershell
dotnet build wt-agent.sln
```

Test:

```powershell
dotnet test wt-agent.sln
```
