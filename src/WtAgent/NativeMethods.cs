using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

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
    public const int SW_RESTORE = 9;
    public const int WM_CLOSE = 0x0010;
    public const int WM_MOUSEWHEEL = 0x020A;
    public const uint PW_RENDERFULLCONTENT = 0x00000002;
    public const int WHEEL_DELTA = 120;
    public const byte KEYEVENTF_KEYUP = 0x0002;
    public const byte VK_CONTROL = 0x11;
    public const byte VK_SHIFT = 0x10;
    public const byte VK_PRIOR = 0x21;
    public const byte VK_NEXT = 0x22;
    public const byte VK_HOME = 0x24;
    public const byte VK_END = 0x23;
    public const byte VK_RETURN = 0x0D;

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

    public static bool TryGetClientRectangle(IntPtr hwnd, out RECT clientRect)
    {
        clientRect = default;
        if (!GetClientRect(hwnd, out var client))
        {
            return false;
        }

        var origin = new POINT { X = client.Left, Y = client.Top };
        if (!ClientToScreen(hwnd, ref origin))
        {
            return false;
        }

        clientRect = new RECT
        {
            Left = origin.X,
            Top = origin.Y,
            Right = origin.X + (client.Right - client.Left),
            Bottom = origin.Y + (client.Bottom - client.Top)
        };
        return true;
    }

    public static void ScrollWindowPage(IntPtr hwnd, bool up)
    {
        ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
        Thread.Sleep(80);

        if (!TrySendKeys(up ? "^+{PGUP}" : "^+{PGDN}"))
        {
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, 0, UIntPtr.Zero);
            keybd_event(up ? VK_PRIOR : VK_NEXT, 0, 0, UIntPtr.Zero);
            keybd_event(up ? VK_PRIOR : VK_NEXT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    public static void PasteCommandAndSubmit(IntPtr hwnd, string commandText)
    {
        PasteText(hwnd, commandText);
        Submit(hwnd);
    }

    public static void PasteText(IntPtr hwnd, string text)
    {
        ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
        Thread.Sleep(120);
        SetClipboardText(text);
        Thread.Sleep(80);

        if (!TrySendKeys("^v"))
        {
            throw new InvalidOperationException("Failed to paste text into Windows Terminal.");
        }
    }

    public static void TypeText(IntPtr hwnd, string text)
    {
        ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
        Thread.Sleep(120);

        if (!TrySendKeys(EscapeForSendKeys(text)))
        {
            throw new InvalidOperationException("Failed to type text into Windows Terminal.");
        }
    }

    public static void Submit(IntPtr hwnd)
    {
        ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
        Thread.Sleep(50);

        if (!TrySendKeys("{ENTER}"))
        {
            keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    public static void SendCtrlC(IntPtr hwnd)
    {
        ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
        Thread.Sleep(80);

        if (!TrySendKeys("^c"))
        {
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(0x43, 0, 0, UIntPtr.Zero);
            keybd_event(0x43, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    public static void ScrollWindowToBoundary(IntPtr hwnd, bool top)
    {
        ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
        Thread.Sleep(80);

        if (!TrySendKeys(top ? "^+{HOME}" : "^+{END}"))
        {
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, 0, UIntPtr.Zero);
            keybd_event(top ? VK_HOME : VK_END, 0, 0, UIntPtr.Zero);
            keybd_event(top ? VK_HOME : VK_END, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    public static void ScrollWindowWheel(IntPtr hwnd, bool down, int detents)
    {
        if (detents <= 0)
        {
            return;
        }

        ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
        Thread.Sleep(80);

        if (!GetWindowRect(hwnd, out var rect))
        {
            return;
        }

        var x = rect.Left + Math.Max(8, (rect.Right - rect.Left) / 2);
        var y = rect.Top + Math.Max(8, (rect.Bottom - rect.Top) / 2);
        var lParam = CreateLParam(x, y);
        var wheelDelta = (down ? -WHEEL_DELTA : WHEEL_DELTA) << 16;

        for (var i = 0; i < detents; i++)
        {
            PostMessage(hwnd, WM_MOUSEWHEEL, (IntPtr)wheelDelta, lParam);
            Thread.Sleep(35);
        }
    }

    private static bool TrySendKeys(string keys)
    {
        Exception? failure = null;
        using var completed = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                SendKeys.SendWait(keys);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                completed.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        completed.Wait(TimeSpan.FromSeconds(2));
        return failure is null;
    }

    private static void SetClipboardText(string text)
    {
        Exception? failure = null;
        using var completed = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                completed.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        completed.Wait(TimeSpan.FromSeconds(2));

        if (failure is not null)
        {
            throw failure;
        }
    }

    private static string EscapeForSendKeys(string text)
    {
        var builder = new StringBuilder(text.Length * 2);
        foreach (var character in text)
        {
            builder.Append(character switch
            {
                '+' => "{+}",
                '^' => "{^}",
                '%' => "{%}",
                '~' => "{~}",
                '(' => "{(}",
                ')' => "{)}",
                '{' => "{{}",
                '}' => "{}}",
                '[' => "{[}",
                ']' => "{]}",
                _ => character.ToString()
            });
        }

        return builder.ToString();
    }

    private static IntPtr CreateLParam(int low, int high)
    {
        var value = ((high & 0xFFFF) << 16) | (low & 0xFFFF);
        return (IntPtr)value;
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

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

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

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }
}
