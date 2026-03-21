using System.Text;

namespace WtAgent;

internal static class WslSessionScriptWriter
{
    public static void Write(SessionLayout layout, SessionManifest manifest)
    {
        var promptStatePath = ToWslPath(layout.NestedPromptStateFilePath);
        var transcriptPath = ToWslPath(layout.NestedTranscriptPath);
        var readyPath = ToWslPath(Path.Combine(layout.SessionDirectory, "nested-ready.json"));
        var linuxWorkingDirectory = ToWslPath(manifest.WorkingDirectory);
        var rcPath = ToWslPath(layout.WslRcFilePath);
        var bootstrapPath = layout.WslBootstrapScriptPath;

        File.WriteAllText(layout.WslRcFilePath, BuildRcFile(promptStatePath, transcriptPath, readyPath, linuxWorkingDirectory), new UTF8Encoding(false));
        File.WriteAllText(bootstrapPath, BuildBootstrapScript(rcPath), new UTF8Encoding(false));
    }

    private static string BuildBootstrapScript(string rcPath)
    {
        return $$"""
#!/usr/bin/env bash
set -euo pipefail
exec bash --noprofile --rcfile '{{rcPath}}' -i
""";
    }

    private static string BuildRcFile(string promptStatePath, string transcriptPath, string readyPath, string linuxWorkingDirectory)
    {
        return $$"""
export WT_AGENT_NESTED_PROMPT_STATE_PATH='{{EscapeSingleQuoted(promptStatePath)}}'
export WT_AGENT_NESTED_TRANSCRIPT_PATH='{{EscapeSingleQuoted(transcriptPath)}}'
export WT_AGENT_NESTED_READY_PATH='{{EscapeSingleQuoted(readyPath)}}'
export WT_AGENT_NESTED_CWD='{{EscapeSingleQuoted(linuxWorkingDirectory)}}'
export WT_AGENT_PROMPT_SERIAL=0
export WT_AGENT_LAST_HISTORY=0
export HISTFILE=/dev/null
history -c

if [[ -n "${WT_AGENT_NESTED_CWD:-}" && -d "${WT_AGENT_NESTED_CWD:-}" ]]; then
  cd "${WT_AGENT_NESTED_CWD}"
fi

if [[ -z "${WT_AGENT_TEE_ACTIVE:-}" ]]; then
  export WT_AGENT_TEE_ACTIVE=1
  exec > >(tee -a "${WT_AGENT_NESTED_TRANSCRIPT_PATH}") 2>&1
fi

wt_agent_write_prompt_state() {
  local exit_code="$1"
  local cwd="$PWD"
  local host_name
  host_name="$(hostname 2>/dev/null || echo wsl)"
  local prompt="${USER:-user}@${host_name}:${cwd}$ "
  python3 - "${WT_AGENT_NESTED_PROMPT_STATE_PATH}" "${WT_AGENT_PROMPT_SERIAL}" "${exit_code}" "${cwd}" "${prompt}" <<'PY'
import json
import sys
from datetime import datetime, timezone

path, serial, exit_code, cwd, prompt = sys.argv[1:]
payload = {
    "promptSerial": int(serial),
    "lastExitCode": int(exit_code),
    "cwd": cwd,
    "prompt": prompt,
    "updatedAtUtc": datetime.now(timezone.utc).isoformat(),
}

with open(path, "w", encoding="utf-8") as handle:
    json.dump(payload, handle)
PY

  if [[ ! -f "${WT_AGENT_NESTED_READY_PATH}" ]]; then
    python3 - "${WT_AGENT_NESTED_READY_PATH}" <<'PY'
import json
import sys
from datetime import datetime, timezone

with open(sys.argv[1], "w", encoding="utf-8") as handle:
    json.dump({
        "ready": True,
        "updatedAtUtc": datetime.now(timezone.utc).isoformat(),
    }, handle)
PY
  fi

  printf '%s\n' "${prompt}" >> "${WT_AGENT_NESTED_TRANSCRIPT_PATH}"
}

wt_agent_prompt_command() {
  local exit_code="$?"
  local history_line
  history_line="$(history 1 2>/dev/null || true)"
  local history_number
  history_number="$(printf '%s\n' "${history_line}" | sed -E 's/^[[:space:]]*([0-9]+).*/\1/')"
  if [[ "${history_number}" =~ ^[0-9]+$ ]] && (( history_number > WT_AGENT_LAST_HISTORY )); then
    local command_text
    command_text="$(printf '%s\n' "${history_line}" | sed -E 's/^[[:space:]]*[0-9]+[[:space:]]+//')"
    printf '%s\n' "${command_text}" >> "${WT_AGENT_NESTED_TRANSCRIPT_PATH}"
    WT_AGENT_LAST_HISTORY="${history_number}"
  fi

  WT_AGENT_PROMPT_SERIAL=$((WT_AGENT_PROMPT_SERIAL + 1))
  wt_agent_write_prompt_state "${exit_code}"
}

PROMPT_COMMAND=wt_agent_prompt_command
PS1='\u@\h:\w\$ '
""";
    }

    private static string EscapeSingleQuoted(string input) => input.Replace("'", "'\"'\"'", StringComparison.Ordinal);

    private static string ToWslPath(string windowsPath)
    {
        var fullPath = Path.GetFullPath(windowsPath);
        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var driveLetter = root.TrimEnd('\\', ':').ToLowerInvariant();
        var remainder = fullPath[root.Length..].Replace('\\', '/');
        return $"/mnt/{driveLetter}/{remainder}";
    }
}
