using System.Drawing;
using System.Drawing.Imaging;

namespace WtAgent;

internal static class ScrollCaptureStitcher
{
    public static string ComputeFingerprint(Bitmap bitmap)
    {
        unchecked
        {
            var hash = 17;
            var sampleX = Math.Max(1, bitmap.Width / 32);
            var sampleY = Math.Max(1, bitmap.Height / 32);

            for (var y = 0; y < bitmap.Height; y += sampleY)
            {
                for (var x = 0; x < bitmap.Width; x += sampleX)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    hash = (hash * 31) + pixel.ToArgb();
                }
            }

            return hash.ToString("X8");
        }
    }

    public static Bitmap Stitch(IReadOnlyList<Bitmap> frames)
    {
        if (frames.Count == 0)
        {
            throw new ArgumentException("At least one frame is required.", nameof(frames));
        }

        if (frames.Count == 1)
        {
            return (Bitmap)frames[0].Clone();
        }

        var overlaps = new int[frames.Count - 1];
        var totalHeight = frames[0].Height;
        var width = frames.Max(bitmap => bitmap.Width);

        for (var i = 1; i < frames.Count; i++)
        {
            overlaps[i - 1] = FindVerticalOverlap(frames[i - 1], frames[i]);
            totalHeight += frames[i].Height - overlaps[i - 1];
        }

        var canvas = new Bitmap(width, totalHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(canvas);
        graphics.Clear(Color.Black);

        var y = 0;
        graphics.DrawImageUnscaled(frames[0], 0, y);
        y += frames[0].Height;

        for (var i = 1; i < frames.Count; i++)
        {
            var overlap = overlaps[i - 1];
            var source = new Rectangle(0, overlap, frames[i].Width, frames[i].Height - overlap);
            var destination = new Rectangle(0, y - overlap, source.Width, source.Height);
            graphics.DrawImage(frames[i], destination, source, GraphicsUnit.Pixel);
            y += frames[i].Height - overlap;
        }

        return canvas;
    }

    public static int FindVerticalOverlap(Bitmap previous, Bitmap current)
    {
        var minOverlap = Math.Min(24, Math.Min(previous.Height, current.Height) / 8);
        var maxOverlap = Math.Min(previous.Height, current.Height) - 1;

        for (var overlap = maxOverlap; overlap >= minOverlap; overlap--)
        {
            if (AreSlicesEquivalent(previous, previous.Height - overlap, current, 0, overlap))
            {
                return overlap;
            }
        }

        return 0;
    }

    private static bool AreSlicesEquivalent(Bitmap previous, int previousStartY, Bitmap current, int currentStartY, int height)
    {
        var width = Math.Min(previous.Width, current.Width);
        var sampleX = Math.Max(1, width / 48);
        var sampleY = Math.Max(1, height / 24);
        long delta = 0;
        long tolerance = 24L * ((width / sampleX) + 1) * ((height / sampleY) + 1);

        for (var y = 0; y < height; y += sampleY)
        {
            for (var x = 0; x < width; x += sampleX)
            {
                var prev = previous.GetPixel(x, previousStartY + y);
                var next = current.GetPixel(x, currentStartY + y);
                delta += Math.Abs(prev.R - next.R) + Math.Abs(prev.G - next.G) + Math.Abs(prev.B - next.B);
                if (delta > tolerance)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
