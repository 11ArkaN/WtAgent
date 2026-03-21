using System.Drawing;
using WtAgent;

namespace WtAgent.Tests;

public sealed class ScrollCaptureStitcherTests
{
    [Fact]
    public void FindVerticalOverlap_DetectsSharedRows()
    {
        using var first = CreateFrame(startValue: 0, totalRows: 6);
        using var second = CreateFrame(startValue: 4, totalRows: 6);

        var overlap = ScrollCaptureStitcher.FindVerticalOverlap(first, second);
        Assert.Equal(2, overlap);
    }

    [Fact]
    public void Stitch_StacksFramesWithoutDroppingRows()
    {
        using var first = CreateFrame(startValue: 0, totalRows: 6);
        using var second = CreateFrame(startValue: 4, totalRows: 6);

        using var stitched = ScrollCaptureStitcher.Stitch([first, second]);
        Assert.Equal(12, stitched.Height);
        Assert.Equal(first.Width, stitched.Width);
    }

    private static Bitmap CreateFrame(int startValue, int totalRows)
    {
        var bitmap = new Bitmap(24, totalRows);
        using var graphics = Graphics.FromImage(bitmap);
        for (var y = 0; y < totalRows; y++)
        {
            var value = startValue + y;
            using var brush = new SolidBrush(Color.FromArgb((value * 37) % 255, (value * 91) % 255, (value * 53) % 255));
            graphics.FillRectangle(brush, 0, y, bitmap.Width, 1);
        }

        return bitmap;
    }
}
