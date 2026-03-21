# Command Patterns

## Install From GitHub Release

1. Open the latest release page.
2. Download `WtAgent-Setup.exe`.
3. Run the setup wizard and select the skill tasks you want.

That is the primary end-user install path.

## Install From Local Repo Scripts

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-wt-agent.ps1 -InstallSkill
```

Use this only for local development or unpublished branches.

## Install Through skills.sh

```bash
npx skills add . --skill wt-agent-terminal
```

Install from the current checkout into an agent detected by the `skills` CLI.

```bash
npx skills add https://github.com/11ArkaN/WtAgent --skill wt-agent-terminal --agent codex
npx skills add https://github.com/11ArkaN/WtAgent --skill wt-agent-terminal --agent claude-code
```

Use this path when the target agent is not Codex or when you want the standard multi-agent skills ecosystem instead of the Windows-only convenience installer.

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
