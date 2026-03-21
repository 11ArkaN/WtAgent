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

        using var graphicsCaptureAttempt = TryGraphicsCapture(hwnd);
        if (graphicsCaptureAttempt is not null && !IsMostlyBlank(graphicsCaptureAttempt))
        {
            graphicsCaptureAttempt.Save(outputPath, ImageFormat.Png);
            return new CaptureResult("graphics-capture", false);
        }

        var blankFrameDetected = graphicsCaptureAttempt is not null;
        using var fallback = CaptureWithPrintWindow(hwnd);
        if (fallback is null)
        {
            return new CaptureResult("none", blankFrameDetected);
        }

        var fallbackBlank = IsMostlyBlank(fallback);
        fallback.Save(outputPath, ImageFormat.Png);
        return new CaptureResult("fallback-printwindow", blankFrameDetected || fallbackBlank);
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
}
