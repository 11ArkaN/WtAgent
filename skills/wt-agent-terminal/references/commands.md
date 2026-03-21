# Command Patterns

## Install From GitHub Release

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-wt-agent.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\install-wt-agent-skill.ps1
```

Use this as the default install path. It downloads the latest release assets from `11ArkaN/WtAgent`, installs the CLI into `%LOCALAPPDATA%\wt-agent\current`, and installs the skill into `%CODEX_HOME%\skills\wt-agent-terminal` or `%USERPROFILE%\.codex\skills\wt-agent-terminal`.

## Install Local Unreleased Build

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-wt-agent.ps1 -Source local
powershell -ExecutionPolicy Bypass -File .\scripts\install-wt-agent-skill.ps1 -Source local
```

Use this only when testing a branch that has not been published as a release yet.

## One-shot Command

```powershell
wt-agent run --command "curl.exe -v https://httpbin.org/get" --profile "Windows PowerShell" --cwd "C:\work"
```

Use this for a single command when one final screenshot is enough.

## Interactive Session

```powershell
wt-agent session-start --profile "Windows PowerShell" --cwd "C:\work"
wt-agent session-send --session-id <id> --input "git status"
wt-agent session-send --session-id <id> --input "curl.exe -v https://httpbin.org/get" --wait-mode prompt --capture true
wt-agent session-stop --session-id <id>
```

Use this for multi-step work.

## Staged Input Then Submit

```powershell
wt-agent session-send --session-id <id> --input "npm test -- --watch" --submit false --capture true
wt-agent session-submit --session-id <id> --wait-mode none --capture true
```

Use this when the command should be visible in the terminal before submission.

## Enter WSL

```powershell
wt-agent session-enter-wsl --session-id <id>
wt-agent session-send --session-id <id> --input "pwd"
wt-agent session-send --session-id <id> --input "curl -v https://httpbin.org/get"
```

Prefer this over manually typing `wsl` when you want reliable nested-shell state.

## Recover Lost Context

```powershell
wt-agent session-list
wt-agent session-inspect --session-id <id>
wt-agent session-status --session-id <id>
```

Use this when the agent forgets the session id or restarts context.

## Interrupt And Capture

```powershell
wt-agent session-interrupt --session-id <id> --capture true
wt-agent session-capture --session-id <id> --label "after-interrupt"
```

Use this for long-running or stuck commands.
