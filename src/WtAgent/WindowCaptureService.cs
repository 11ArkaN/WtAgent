using System.Drawing;
using System.Drawing.Imaging;

namespace WtAgent;

internal sealed class WindowCaptureService
{
    public CaptureResult Capture(IntPtr hwnd, string outputPath)
    {
        if (hwnd == IntPtr.Zero)
        {
            return new CaptureResult("none", false);
        }

        var originalForeground = NativeMethods.GetForegroundWindow();
        var framesToDispose = new List<CapturedFrame>();
        try
        {
            using var initial = CaptureFrame(hwnd);
            if (initial is null)
            {
                return new CaptureResult("none", false);
            }

            var anyBlank = initial.IsBlank;
            if (initial.ClientBitmap is null)
            {
                initial.FullBitmap.Save(outputPath, ImageFormat.Png);
                return new CaptureResult(initial.ModeUsed, anyBlank);
            }

            var frames = new List<CapturedFrame>();
            using var topFrame = MoveToTop(hwnd, initial.ModeUsed);
            if (topFrame is null || topFrame.ClientBitmap is null)
            {
                initial.FullBitmap.Save(outputPath, ImageFormat.Png);
                return new CaptureResult(initial.ModeUsed, anyBlank);
            }

            anyBlank |= topFrame.IsBlank;
            var detachedTop = topFrame.Detach();
            frames.Add(detachedTop);
            framesToDispose.Add(detachedTop);
            var bottomFingerprint = ScrollCaptureStitcher.ComputeFingerprint(initial.ClientBitmap);
            var repeatedFrames = 0;

            for (var i = 0; i < 256; i++)
            {
                NativeMethods.ScrollWindowPage(hwnd, up: false);
                Thread.Sleep(140);

                using var nextFrame = CaptureFrame(hwnd, initial.ModeUsed);
                if (nextFrame is null || nextFrame.ClientBitmap is null)
                {
                    break;
                }

                anyBlank |= nextFrame.IsBlank;
                var previous = frames[^1];
                var nextClient = nextFrame.ClientBitmap!;
                var previousClient = previous.ClientBitmap!;
                var nextFingerprint = ScrollCaptureStitcher.ComputeFingerprint(nextClient);
                var previousFingerprint = ScrollCaptureStitcher.ComputeFingerprint(previousClient);

                if (nextFingerprint == previousFingerprint)
                {
                    repeatedFrames++;
                    if (repeatedFrames >= 2)
                    {
                        break;
                    }

                    continue;
                }

                repeatedFrames = 0;
                var detached = nextFrame.Detach();
                frames.Add(detached);
                framesToDispose.Add(detached);

                if (nextFingerprint == bottomFingerprint)
                {
                    break;
                }
            }

            if (frames.Count == 1)
            {
                initial.FullBitmap.Save(outputPath, ImageFormat.Png);
                return new CaptureResult(initial.ModeUsed, anyBlank);
            }

            using var stitched = ScrollCaptureStitcher.Stitch(frames.Select(frame => frame.ClientBitmap!).ToList());
            stitched.Save(outputPath, ImageFormat.Png);
            return new CaptureResult($"scroll-stitch+{initial.ModeUsed}", anyBlank);
        }
        finally
        {
            foreach (var frame in framesToDispose)
            {
                frame.Dispose();
            }

            if (originalForeground != IntPtr.Zero && originalForeground != hwnd)
            {
                NativeMethods.SetForegroundWindow(originalForeground);
            }
        }
    }

    private static CapturedFrame? MoveToTop(IntPtr hwnd, string preferredMode)
    {
        CapturedFrame? current = CaptureFrame(hwnd, preferredMode);
        if (current is null)
        {
            return null;
        }

        var stagnant = 0;
        for (var i = 0; i < 256; i++)
        {
            var previousFingerprint = current.ClientBitmap is null
                ? string.Empty
                : ScrollCaptureStitcher.ComputeFingerprint(current.ClientBitmap);

            NativeMethods.ScrollWindowPage(hwnd, up: true);
            Thread.Sleep(140);

            using var next = CaptureFrame(hwnd, preferredMode);
            if (next is null || next.ClientBitmap is null)
            {
                break;
            }

            var nextFingerprint = ScrollCaptureStitcher.ComputeFingerprint(next.ClientBitmap);
            if (nextFingerprint == previousFingerprint)
            {
                stagnant++;
                if (stagnant >= 2)
                {
                    break;
                }

                continue;
            }

            stagnant = 0;
            current.Dispose();
            current = next.Detach();
        }

        return current;
    }

    private static CapturedFrame? CaptureFrame(IntPtr hwnd, string? preferredMode = null)
    {
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        using var graphicsCaptureAttempt = preferredMode is null ? TryGraphicsCapture(hwnd) : null;
        if (graphicsCaptureAttempt is not null && !IsMostlyBlank(graphicsCaptureAttempt))
        {
            return CreateFrame(hwnd, graphicsCaptureAttempt, "graphics-capture", false);
        }

        var blankFrameDetected = graphicsCaptureAttempt is not null;
        var modeUsed = preferredMode == "graphics-capture" ? "graphics-capture" : "fallback-printwindow";
        var fallback = CaptureWithPrintWindow(hwnd);
        if (fallback is null)
        {
            return null;
        }

        var fallbackBlank = IsMostlyBlank(fallback);
        return CreateFrame(hwnd, fallback, modeUsed, blankFrameDetected || fallbackBlank);
    }

    private static CapturedFrame CreateFrame(IntPtr hwnd, Bitmap fullBitmap, string modeUsed, bool isBlank)
    {
        Bitmap? clientBitmap = null;
        if (NativeMethods.TryGetClientRectangle(hwnd, out var clientRect) && NativeMethods.GetWindowRect(hwnd, out var windowRect))
        {
            var x = Math.Max(0, clientRect.Left - windowRect.Left);
            var y = Math.Max(0, clientRect.Top - windowRect.Top);
            var width = Math.Min(fullBitmap.Width - x, clientRect.Right - clientRect.Left);
            var height = Math.Min(fullBitmap.Height - y, clientRect.Bottom - clientRect.Top);

            if (width > 0 && height > 0)
            {
                clientBitmap = fullBitmap.Clone(new Rectangle(x, y, width, height), PixelFormat.Format32bppArgb);
            }
        }

        return new CapturedFrame(fullBitmap, clientBitmap, modeUsed, isBlank);
    }

    private static Bitmap? TryGraphicsCapture(IntPtr hwnd)
    {
        return null;
    }

    private static Bitmap? CaptureWithPrintWindow(IntPtr hwnd)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return null;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var hdc = graphics.GetHdc();
        var released = false;
        try
        {
            var ok = NativeMethods.PrintWindow(hwnd, hdc, NativeMethods.PW_RENDERFULLCONTENT);
            if (ok)
            {
                return bitmap;
            }

            graphics.ReleaseHdc(hdc);
            released = true;
            using var fallbackGraphics = Graphics.FromImage(bitmap);
            fallbackGraphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
            return bitmap;
        }
        finally
        {
            if (!released)
            {
                graphics.ReleaseHdc(hdc);
            }
        }
    }

    private static bool IsMostlyBlank(Bitmap bitmap)
    {
        long variance = 0;
        var sampleStride = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 32);
        var first = bitmap.GetPixel(0, 0);

        for (var y = 0; y < bitmap.Height; y += sampleStride)
        {
            for (var x = 0; x < bitmap.Width; x += sampleStride)
            {
                var pixel = bitmap.GetPixel(x, y);
                variance += Math.Abs(pixel.R - first.R) + Math.Abs(pixel.G - first.G) + Math.Abs(pixel.B - first.B);
                if (variance > 1024)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private sealed class CapturedFrame : IDisposable
    {
        public CapturedFrame(Bitmap fullBitmap, Bitmap? clientBitmap, string modeUsed, bool isBlank)
        {
            FullBitmap = fullBitmap;
            ClientBitmap = clientBitmap;
            ModeUsed = modeUsed;
            IsBlank = isBlank;
        }

        public Bitmap FullBitmap { get; private set; }

        public Bitmap? ClientBitmap { get; private set; }

        public string ModeUsed { get; }

        public bool IsBlank { get; }

        public CapturedFrame Detach()
        {
            var detached = new CapturedFrame(FullBitmap, ClientBitmap, ModeUsed, IsBlank);
            FullBitmap = null!;
            ClientBitmap = null;
            return detached;
        }

        public void Dispose()
        {
            FullBitmap?.Dispose();
            ClientBitmap?.Dispose();
        }
    }
}
