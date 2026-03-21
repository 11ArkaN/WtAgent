using System.Diagnostics;
using System.Text;

namespace WtAgent;

internal static class WindowsTerminalLauncher
{
    public static LaunchResult Launch(RunArguments arguments, RunLayout layout, TerminalProfile profile, string title)
    {
        return LaunchInternal(profile.Name, arguments.WorkingDirectory, title, layout.BootstrapScriptPath);
    }

    public static LaunchResult LaunchSession(StartSessionArguments arguments, SessionLayout layout, TerminalProfile profile, string title)
    {
        return LaunchInternal(profile.Name, arguments.WorkingDirectory, title, layout.BootstrapScriptPath);
    }

    private static LaunchResult LaunchInternal(string profileName, string workingDirectory, string title, string bootstrapPath)
    {
        var bootstrapShell = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
        var wtPath = ResolveWtPath();
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = wtPath,
            Arguments = BuildArguments(profileName, workingDirectory, title, bootstrapShell, bootstrapPath),
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Failed to start wt.exe.");

        return new LaunchResult(process.Id, process.Handle, IntPtr.Zero, string.Empty);
    }

    private static string BuildArguments(string profileName, string cwd, string title, string bootstrapShell, string bootstrapPath)
    {
        var builder = new StringBuilder();
        builder.Append("-w new new-tab");
        builder.Append(" --title ").Append(Quote(title));
        builder.Append(" -p ").Append(Quote(profileName));
        builder.Append(" -d ").Append(Quote(cwd));
        builder.Append(' ').Append(Quote(bootstrapShell));
        builder.Append(" -NoLogo -NoProfile -NoExit -ExecutionPolicy Bypass -File ");
        builder.Append(Quote(bootstrapPath));
        return builder.ToString();
    }

    private static string ResolveWtPath()
    {
        var localWt = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps",
            "wt.exe");

        if (File.Exists(localWt))
        {
            return localWt;
        }

        throw new InvalidOperationException("wt.exe was not found in the current user profile.");
    }

    private static string Quote(string value) => $"\"{value}\"";
}
