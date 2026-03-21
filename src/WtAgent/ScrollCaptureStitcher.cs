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

    public static int CountDistinctRows(Bitmap bitmap)
    {
        return ComputeRowHashes(bitmap).Distinct().Count();
    }

    public static int CountSharedTopRows(Bitmap first, Bitmap second, int maxRows)
    {
        var firstHashes = ComputeRowHashes(first);
        var secondHashes = ComputeRowHashes(second);
        var limit = Math.Min(maxRows, Math.Min(firstHashes.Length, secondHashes.Length));
        var shared = 0;

        for (var index = 0; index < limit; index++)
        {
            if (firstHashes[index] != secondHashes[index])
            {
                break;
            }

            shared++;
        }

        return shared;
    }

    public static Bitmap Stitch(IReadOnlyList<Bitmap> frames)
    {
        return Stack(frames);
    }

    public static Bitmap Stack(IReadOnlyList<Bitmap> frames)
    {
        if (frames.Count == 0)
        {
            throw new ArgumentException("At least one frame is required.", nameof(frames));
        }

        if (frames.Count == 1)
        {
            return (Bitmap)frames[0].Clone();
        }

        var totalHeight = frames[0].Height;
        var width = frames.Max(bitmap => bitmap.Width);

        for (var i = 1; i < frames.Count; i++)
        {
            totalHeight += frames[i].Height;
        }

        var canvas = new Bitmap(width, totalHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(canvas);
        graphics.Clear(Color.Black);

        var y = 0;
        graphics.DrawImageUnscaled(frames[0], 0, y);
        y += frames[0].Height;

        for (var i = 1; i < frames.Count; i++)
        {
            graphics.DrawImageUnscaled(frames[i], 0, y);
            y += frames[i].Height;
        }

        return canvas;
    }

    public static int[] ComputeOverlaps(IReadOnlyList<Bitmap> frames)
    {
        if (frames.Count <= 1)
        {
            return [];
        }

        var overlaps = new int[frames.Count - 1];
        for (var i = 1; i < frames.Count; i++)
        {
            overlaps[i - 1] = FindVerticalOverlap(frames[i - 1], frames[i]);
        }

        return overlaps;
    }

    public static int FindVerticalOverlap(Bitmap previous, Bitmap current)
    {
        var minDimension = Math.Min(previous.Height, current.Height);
        var maxOverlap = Math.Min(previous.Height, current.Height) - 1;
        var minOverlap = Math.Min(Math.Max(2, minDimension / 20), maxOverlap);
        var previousHashes = ComputeRowHashes(previous);
        var currentHashes = ComputeRowHashes(current);

        for (var overlap = maxOverlap; overlap >= minOverlap; overlap--)
        {
            if (RowsOverlapExactly(previousHashes, currentHashes, overlap))
            {
                return overlap;
            }
        }

        var bestOverlap = 0;
        var bestScore = double.MaxValue;

        for (var overlap = maxOverlap; overlap >= minOverlap; overlap--)
        {
            var exactRatio = ComputeExactRowMatchRatio(previousHashes, currentHashes, overlap);
            if (exactRatio < 0.08 && overlap > minOverlap * 2)
            {
                continue;
            }

            var score = ComputeOverlapScore(previous, current, overlap);
            if (score < bestScore - 0.01 || (Math.Abs(score - bestScore) <= 0.01 && overlap > bestOverlap))
            {
                bestScore = score;
                bestOverlap = overlap;
            }
        }

        return bestScore <= 8.0 ? bestOverlap : 0;
    }

    private static uint[] ComputeRowHashes(Bitmap bitmap)
    {
        var hashes = new uint[bitmap.Height];
        unchecked
        {
            var usableWidth = Math.Max(1, bitmap.Width - 28);
            for (var y = 0; y < bitmap.Height; y++)
            {
                uint hash = 2166136261;
                for (var x = 0; x < usableWidth; x++)
                {
                    hash ^= (uint)bitmap.GetPixel(x, y).ToArgb();
                    hash *= 16777619;
                }

                hashes[y] = hash;
            }
        }

        return hashes;
    }

    private static double ComputeExactRowMatchRatio(uint[] previous, uint[] current, int overlap)
    {
        var matches = 0;
        for (var index = 0; index < overlap; index++)
        {
            if (previous[previous.Length - overlap + index] != current[index])
            {
                continue;
            }

            matches++;
        }

        return (double)matches / overlap;
    }

    private static bool RowsOverlapExactly(uint[] previous, uint[] current, int overlap)
    {
        for (var index = 0; index < overlap; index++)
        {
            if (previous[previous.Length - overlap + index] != current[index])
            {
                return false;
            }
        }

        return true;
    }

    private static double ComputeOverlapScore(Bitmap previous, Bitmap current, int overlap)
    {
        var rowStep = Math.Max(1, overlap / 48);
        var columnLimit = Math.Max(1, Math.Min(previous.Width, current.Width) - 28);
        var columnStep = Math.Max(2, columnLimit / 96);
        long diffSum = 0;
        var samples = 0;

        for (var offset = 0; offset < overlap; offset += rowStep)
        {
            var previousY = previous.Height - overlap + offset;
            var currentY = offset;

            for (var x = 0; x < columnLimit; x += columnStep)
            {
                var previousPixel = previous.GetPixel(x, previousY);
                var currentPixel = current.GetPixel(x, currentY);
                diffSum += Math.Abs(previousPixel.R - currentPixel.R);
                diffSum += Math.Abs(previousPixel.G - currentPixel.G);
                diffSum += Math.Abs(previousPixel.B - currentPixel.B);
                samples += 3;
            }
        }

        return samples == 0 ? double.MaxValue : (double)diffSum / samples;
    }
}
