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
            using var initial = CaptureSettledFrame(hwnd);
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
            var repeatedFrames = 0;

            for (var i = 0; i < 256; i++)
            {
                NativeMethods.ScrollWindowPage(hwnd, up: false);
                Thread.Sleep(260);

                using var nextFrame = CaptureSettledFrame(hwnd, initial.ModeUsed);
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
            }

            if (frames.Count == 1)
            {
                initial.FullBitmap.Save(outputPath, ImageFormat.Png);
                return new CaptureResult(initial.ModeUsed, anyBlank);
            }

            SaveDebugFrames(outputPath, frames);
            using var preparedFrames = PrepareFramesForStitch(frames);
            SavePreparedFrames(outputPath, preparedFrames);
            SaveOverlapMetrics(outputPath, preparedFrames);
            using var stitched = ScrollCaptureStitcher.Stitch(preparedFrames);
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

    private static PreparedFrameSet PrepareFramesForStitch(IReadOnlyList<CapturedFrame> frames)
    {
        var prepared = new List<Bitmap>(frames.Count)
        {
            (Bitmap)frames[0].ClientBitmap!.Clone()
        };

        var maxSharedTopRows = Math.Min(160, frames[0].ClientBitmap!.Height / 4);
        var sharedTopRows = frames.Count > 1
            ? ScrollCaptureStitcher.CountSharedTopRows(frames[0].ClientBitmap!, frames[1].ClientBitmap!, maxSharedTopRows)
            : 0;

        for (var index = 1; index < frames.Count; index++)
        {
            var source = frames[index].ClientBitmap!;
            if (sharedTopRows > 0 && sharedTopRows < source.Height)
            {
                prepared.Add(source.Clone(
                    new Rectangle(0, sharedTopRows, source.Width, source.Height - sharedTopRows),
                    PixelFormat.Format32bppArgb));
                continue;
            }

            prepared.Add((Bitmap)source.Clone());
        }

        return new PreparedFrameSet(prepared);
    }

    private static void SaveDebugFrames(string outputPath, IReadOnlyList<CapturedFrame> frames)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        var debugDirectory = Path.Combine(outputDirectory, "debug-frames");
        Directory.CreateDirectory(debugDirectory);

        for (var index = 0; index < frames.Count; index++)
        {
            var client = frames[index].ClientBitmap;
            if (client is null)
            {
                continue;
            }

            var framePath = Path.Combine(debugDirectory, $"frame-{index:D3}.png");
            client.Save(framePath, ImageFormat.Png);
        }
    }

    private static void SavePreparedFrames(string outputPath, IReadOnlyList<Bitmap> frames)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        var debugDirectory = Path.Combine(outputDirectory, "debug-prepared");
        Directory.CreateDirectory(debugDirectory);

        for (var index = 0; index < frames.Count; index++)
        {
            var framePath = Path.Combine(debugDirectory, $"prepared-{index:D3}.png");
            frames[index].Save(framePath, ImageFormat.Png);
        }
    }

    private static void SaveOverlapMetrics(string outputPath, IReadOnlyList<Bitmap> frames)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        var overlaps = ScrollCaptureStitcher.ComputeOverlaps(frames);
        var metricsPath = Path.Combine(outputDirectory, "debug-overlaps.txt");
        File.WriteAllLines(metricsPath, overlaps.Select((overlap, index) => $"{index}->{index + 1}: {overlap}"));
    }

    private static CapturedFrame? MoveToTop(IntPtr hwnd, string preferredMode)
    {
        NativeMethods.ScrollWindowToBoundary(hwnd, top: true);
        Thread.Sleep(350);
        return CaptureSettledFrame(hwnd, preferredMode);
    }

    private static CapturedFrame? CaptureSettledFrame(IntPtr hwnd, string? preferredMode = null)
    {
        CapturedFrame? current = CaptureFrame(hwnd, preferredMode);
        if (current is null || current.ClientBitmap is null)
        {
            return current;
        }

        for (var attempt = 0; attempt < 4; attempt++)
        {
            Thread.Sleep(120);
            using var next = CaptureFrame(hwnd, preferredMode ?? current.ModeUsed);
            if (next is null || next.ClientBitmap is null)
            {
                return current;
            }

            var currentBitmap = current.ClientBitmap;
            if (currentBitmap is null)
            {
                return current;
            }

            var currentFingerprint = ScrollCaptureStitcher.ComputeFingerprint(currentBitmap);
            var nextFingerprint = ScrollCaptureStitcher.ComputeFingerprint(next.ClientBitmap);
            if (currentFingerprint == nextFingerprint)
            {
                current.Dispose();
                return next.Detach();
            }

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

    private sealed class PreparedFrameSet : IDisposable, IReadOnlyList<Bitmap>
    {
        private readonly List<Bitmap> _bitmaps;

        public PreparedFrameSet(List<Bitmap> bitmaps)
        {
            _bitmaps = bitmaps;
        }

        public Bitmap this[int index] => _bitmaps[index];

        public int Count => _bitmaps.Count;

        public IEnumerator<Bitmap> GetEnumerator() => _bitmaps.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
            foreach (var bitmap in _bitmaps)
            {
                bitmap.Dispose();
            }
        }
    }
}
