using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Velopack.UI.Helpers;

internal static class AppIconGenerator
{
    private const int IconSize = 256;

    public static void EnsureAppIcons()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var imagesDir = Path.Combine(baseDir, "Images");
            Directory.CreateDirectory(imagesDir);

            var pngPath = Path.Combine(imagesDir, "Application.png");
            var icoPath = Path.Combine(imagesDir, "Application.ico");

            // Always regenerate to keep visuals consistent; flip to conditional if needed
            var pngBytes = RenderPngBytes();
            File.WriteAllBytes(pngPath, pngBytes);
            File.WriteAllBytes(icoPath, BuildIcoFromPng(pngBytes));
        }
        catch
        {
            // non-fatal; icon generation is best-effort
        }
    }

    private static byte[] RenderPngBytes()
    {
        var dpi = 96d;
        var rtb = new RenderTargetBitmap(IconSize, IconSize, dpi, dpi, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            DrawBrandIcon(dc);
        }
        rtb.Render(dv);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static void DrawBrandIcon(DrawingContext dc)
    {
        // Background rounded square (dark gradient) for good contrast in light/dark UIs
        var rect = new Rect(0, 0, IconSize, IconSize);
        var bgRadius = IconSize * 0.15;
        var bgBrush = new LinearGradientBrush(
            Color.FromRgb(0x33, 0x41, 0x5E),
            Color.FromRgb(0x0F, 0x17, 0x2A),
            new Point(0, 0), new Point(1, 1));
        var bgPen = new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)), IconSize * 0.01);
        dc.DrawRoundedRectangle(bgBrush, bgPen, rect, bgRadius, bgRadius);

        // Package box (amber) symbolizing packaging/build artifacts
        // Body
        var boxW = IconSize * 0.45;
        var boxH = IconSize * 0.34;
        var boxX = (IconSize - boxW) / 2;
        var boxY = IconSize * 0.42;
        var boxBody = new Rect(boxX, boxY, boxW, boxH);
        var boxBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)); // amber 500
        var boxStroke = new Pen(new SolidColorBrush(Color.FromRgb(0xB4, 0x6B, 0x00)), IconSize * 0.01);
        dc.DrawRectangle(boxBrush, boxStroke, boxBody);

        // Lid (slightly darker amber) as a trapezoid
        var lidTop = boxY - IconSize * 0.12;
        var lid = new PathFigureCollection
        {
            new PathFigure(new Point(boxX, boxY), new []
            {
                new LineSegment(new Point(boxX + boxW/2, lidTop), true),
                new LineSegment(new Point(boxX + boxW, boxY), true),
            }, true)
        };
        var lidGeo = new PathGeometry(lid);
        var lidBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)); // amber 600
        dc.DrawGeometry(lidBrush, boxStroke, lidGeo);

        // Center seam
        var seamPen = new Pen(new SolidColorBrush(Color.FromArgb(0x60, 0x00, 0x00, 0x00)), IconSize * 0.01);
        dc.DrawLine(seamPen, new Point(boxX + boxW/2, boxY), new Point(boxX + boxW/2, boxY + boxH));

        // Stylized "V" (installer for Velopack) in a bright cool accent for high contrast
        var accent = new SolidColorBrush(Color.FromRgb(0x22, 0xD3, 0xEE)); // cyan 400
        var vStroke = new Pen(new SolidColorBrush(Colors.White), IconSize * 0.055) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var vPath = new PathGeometry(new[]
        {
            new PathFigure(new Point(IconSize*0.30, IconSize*0.35), new []
            {
                new LineSegment(new Point(IconSize*0.45, IconSize*0.58), true),
                new LineSegment(new Point(IconSize*0.72, IconSize*0.26), true),
            }, false)
        });
        // Glow underlay for readability on any background
        var glow = new Pen(new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0x00, 0x00)), IconSize * 0.10) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, glow, vPath);
        dc.DrawGeometry(null, new Pen(accent, IconSize * 0.06) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, vPath);
        dc.DrawGeometry(null, vStroke, vPath);

        // Up-arrow (packaging/upload) subtly embossed into the box
        var arrowSize = IconSize * 0.12;
        var arrowX = boxX + boxW/2;
        var arrowY = boxY + boxH/2 + IconSize*0.02;
        var arrowGeo = new PathGeometry(new[]
        {
            new PathFigure(new Point(arrowX, arrowY - arrowSize/2), new []
            {
                new LineSegment(new Point(arrowX, arrowY + arrowSize/2), true),
                new LineSegment(new Point(arrowX - arrowSize*0.3, arrowY + arrowSize*0.2), true),
                new LineSegment(new Point(arrowX, arrowY - arrowSize/2), true),
                new LineSegment(new Point(arrowX + arrowSize*0.3, arrowY + arrowSize*0.2), true),
            }, true)
        });
        var arrowBrush = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
        var arrowPen = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x00, 0x00)), IconSize * 0.01);
        dc.DrawGeometry(arrowBrush, arrowPen, arrowGeo);
    }

    // Minimal ICO writer that wraps a 256x256 PNG as a single-entry icon.
    private static byte[] BuildIcoFromPng(byte[] png)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // ICONDIR
        bw.Write((ushort)0); // reserved
        bw.Write((ushort)1); // type: icon
        bw.Write((ushort)1); // count

        // ICONDIRENTRY
        bw.Write((byte)0);   // width 0 => 256
        bw.Write((byte)0);   // height 0 => 256
        bw.Write((byte)0);   // color count
        bw.Write((byte)0);   // reserved
        bw.Write((ushort)1); // planes
        bw.Write((ushort)32);// bit count
        bw.Write((uint)png.Length); // bytes in resource
        bw.Write((uint)(6 + 16));   // offset to image data

        // Image data (PNG)
        bw.Write(png);
        bw.Flush();
        return ms.ToArray();
    }
}
