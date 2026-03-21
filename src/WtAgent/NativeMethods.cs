using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace WtAgent;

internal static class NativeMethods
{
    public const uint DESKTOP_CREATEWINDOW = 0x0002;
    public const uint DESKTOP_ENUMERATE = 0x0040;
    public const uint DESKTOP_WRITEOBJECTS = 0x0080;
    public const uint DESKTOP_SWITCHDESKTOP = 0x0100;
    public const uint DESKTOP_READOBJECTS = 0x0001;
    public const uint NORMAL_PRIORITY_CLASS = 0x00000020;
    public const int STARTF_USESHOWWINDOW = 0x00000001;
    public const ushort SW_SHOWMINNOACTIVE = 7;
    public const int WM_CLOSE = 0x0010;
    public const uint PW_RENDERFULLCONTENT = 0x00000002;

    public static IntPtr CreateDesktopForRun(string desktopName)
    {
        return CreateDesktop(desktopName, null, IntPtr.Zero, 0,
            DESKTOP_CREATEWINDOW | DESKTOP_ENUMERATE | DESKTOP_READOBJECTS | DESKTOP_SWITCHDESKTOP | DESKTOP_WRITEOBJECTS,
            IntPtr.Zero);
    }

    public static void CloseDesktopHandle(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
        {
            CloseDesktop(handle);
        }
    }

    public static void CloseDesktopByName(string desktopName)
    {
        var desktop = OpenDesktop(desktopName, 0, false, DESKTOP_ENUMERATE | DESKTOP_READOBJECTS | DESKTOP_WRITEOBJECTS);
        if (desktop != IntPtr.Zero)
        {
            CloseDesktop(desktop);
        }
    }

    public static LaunchResult CreateProcessOnDesktop(string applicationName, string commandLine, string workingDirectory, string desktopName)
    {
        var startup = new STARTUPINFO
        {
            cb = Marshal.SizeOf<STARTUPINFO>(),
            lpDesktop = desktopName,
            dwFlags = STARTF_USESHOWWINDOW,
            wShowWindow = SW_SHOWMINNOACTIVE
        };

        if (!CreateProcess(
                applicationName,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                NORMAL_PRIORITY_CLASS,
                IntPtr.Zero,
                workingDirectory,
                ref startup,
                out var processInformation))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess failed for Windows Terminal.");
        }

        return new LaunchResult(processInformation.dwProcessId, processInformation.hProcess, processInformation.hThread, desktopName);
    }

    public static IntPtr FindWindowOnDesktop(string desktopName, string title, int processId)
    {
        var desktop = OpenDesktop(desktopName, 0, false, DESKTOP_ENUMERATE | DESKTOP_READOBJECTS);
        if (desktop == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            IntPtr found = IntPtr.Zero;
            EnumDesktopWindows(desktop, (hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd))
                {
                    return true;
                }

                GetWindowThreadProcessId(hwnd, out var currentPid);
                var buffer = new StringBuilder(512);
                GetWindowText(hwnd, buffer, buffer.Capacity);
                var text = buffer.ToString();
                if (text.Contains(title, StringComparison.Ordinal) && (processId <= 0 || currentPid == processId || currentPid > 0))
                {
                    found = hwnd;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return found;
        }
        finally
        {
            CloseDesktop(desktop);
        }
    }

    public static IntPtr FindWindowByTitle(string title)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
            {
                return true;
            }

            var buffer = new StringBuilder(512);
            GetWindowText(hwnd, buffer, buffer.Capacity);
            if (buffer.ToString().Contains(title, StringComparison.Ordinal))
            {
                found = hwnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateDesktop(
        string lpszDesktop,
        string? lpszDevice,
        IntPtr pDevmode,
        int dwFlags,
        uint dwDesiredAccess,
        IntPtr lpsa);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumDesktopWindowsProc lpfn, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumDesktopWindowsProc lpfn, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string? lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        [In] ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    private delegate bool EnumDesktopWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public ushort wShowWindow;
        public ushort cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
