using System.Text.Json;

namespace WtAgent;

internal sealed class WindowsTerminalSettings
{
    private readonly string _settingsPath;

    public WindowsTerminalSettings()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages",
            "Microsoft.WindowsTerminal_8wekyb3d8bbwe",
            "LocalState",
            "settings.json");
    }

    public TerminalProfile ResolveProfile(string profileName)
    {
        if (!File.Exists(_settingsPath))
        {
            throw new InvalidOperationException("Windows Terminal settings.json was not found.");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(_settingsPath));
        var profiles = document.RootElement.GetProperty("profiles").GetProperty("list");

        foreach (var item in profiles.EnumerateArray())
        {
            var name = item.GetProperty("name").GetString();
            if (!string.Equals(name, profileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var commandline = item.TryGetProperty("commandline", out var commandlineProperty)
                ? Environment.ExpandEnvironmentVariables(commandlineProperty.GetString() ?? string.Empty)
                : null;

            var source = item.TryGetProperty("source", out var sourceProperty)
                ? sourceProperty.GetString()
                : null;

            return new TerminalProfile
            {
                Name = name!,
                Commandline = commandline,
                Source = source,
                Flavor = DetectFlavor(commandline, source)
            };
        }

        throw new InvalidOperationException($"Windows Terminal profile '{profileName}' was not found.");
    }

    private static ShellFlavor DetectFlavor(string? commandline, string? source)
    {
        var normalized = (commandline ?? string.Empty).ToLowerInvariant();
        var normalizedSource = (source ?? string.Empty).ToLowerInvariant();

        if (normalized.Contains("powershell.exe"))
        {
            return ShellFlavor.WindowsPowerShell;
        }

        if (normalized.Contains("pwsh"))
        {
            return ShellFlavor.Pwsh;
        }

        if (normalized.Contains("cmd.exe"))
        {
            return ShellFlavor.Cmd;
        }

        if (normalized.Contains("bash"))
        {
            return ShellFlavor.Bash;
        }

        if (normalizedSource.Contains("windows.terminal.wsl") || normalizedSource.Contains("ubuntu") || normalizedSource.Contains("debian"))
        {
            return ShellFlavor.Wsl;
        }

        return ShellFlavor.Unknown;
    }
}
