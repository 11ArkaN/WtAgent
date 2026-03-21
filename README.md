# WtAgent

`WtAgent` lets an AI agent use Windows Terminal with real screenshots.

It can run one-shot commands, keep interactive sessions alive, recover lost sessions, interrupt running work, and continue inside WSL while preserving both machine-readable state and actual rendered terminal images.

## Install

Download and run the normal Windows installer:

- [Latest release](https://github.com/11ArkaN/WtAgent/releases/latest)
- main asset: `WtAgent-Setup.exe`

The setup wizard can:

- install `WtAgent` as a normal per-user Windows app
- add `wt-agent` to the user `PATH`
- install the `wt-agent-terminal` skill for supported agents

## Skill Install

This repo also contains a `skills.sh`-compatible skill in `skills/wt-agent-terminal`.

Install it from anywhere with:

```bash
npx skills add 11ArkaN/WtAgent --skill wt-agent-terminal
```

Or:

```bash
npx skills add https://github.com/11ArkaN/WtAgent --skill wt-agent-terminal
```

If you use `npx skills add . --skill wt-agent-terminal`, `.` must be the root of this repo.

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

## Artifacts

By default, `WtAgent` writes `.wt-agent` into the directory where the agent is currently working.

That keeps artifacts separate per project:

```text
project-a/.wt-agent/...
project-b/.wt-agent/...
```

Use `--artifacts-dir` if you want a custom location.

## Releases

Current public assets:

- `WtAgent-Setup.exe`
- `wt-agent-win-x64.zip`
- `wt-agent-terminal-skill.zip`

## Development

```powershell
dotnet build wt-agent.sln
dotnet test wt-agent.sln
```
