using System.Text;

namespace WtAgent;

internal static class WindowLocator
{
    public static async Task<WindowDiscoveryResult> WaitForWindowAsync(string title, string desktopName, int processId, TimeSpan timeout)
    {
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < timeout)
        {
            var hwnd = string.IsNullOrWhiteSpace(desktopName)
                ? IntPtr.Zero
                : NativeMethods.FindWindowOnDesktop(desktopName, title, processId);
            if (hwnd == IntPtr.Zero)
            {
                hwnd = NativeMethods.FindWindowByTitle(title);
            }

            if (hwnd != IntPtr.Zero)
            {
                var buffer = new StringBuilder(512);
                NativeMethods.GetWindowText(hwnd, buffer, buffer.Capacity);
                NativeMethods.GetWindowThreadProcessId(hwnd, out var foundPid);
                return new WindowDiscoveryResult(hwnd, foundPid, buffer.ToString());
            }

            await Task.Delay(250);
        }

        throw new InvalidOperationException($"Timed out waiting for Windows Terminal window '{title}'.");
    }
}
