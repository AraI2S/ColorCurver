// Shared image processing engine. Independent of the application folder name.
// Image curve parameters match the original ManuBrightness V5 / MapVersion 5.
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class ImageEngine
{
    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    private static double Hermite(double x, double x0, double x1,
        double y0, double y1, double slope0, double slope1)
    {
        double width = x1 - x0;
        double t = (x - x0) / width;
        double h00 = 2 * t * t * t - 3 * t * t + 1;
        double h10 = t * t * t - 2 * t * t + t;
        double h01 = -2 * t * t * t + 3 * t * t;
        double h11 = t * t * t - t * t;
        return h00 * y0 + h10 * width * slope0
             + h01 * y1 + h11 * width * slope1;
    }

    public static double EffectiveLevel(double control)
    {
        double c = Math.Max(0.0, Math.Min(1000.0, control));
        double midSlope = (840.0 - 350.0) / (800.0 - 200.0);
        if (c <= 200.0)
            return Hermite(c, 0.0, 200.0, 0.0, 350.0, 2.5, midSlope);
        if (c <= 800.0)
            return Lerp(350.0, 840.0, (c - 200.0) / 600.0);
        double t = (c - 800.0) / 200.0;
        return 840.0 + 160.0 * (1.0 - Math.Pow(1.0 - t, 2.0));
    }

    public static string Describe(int level)
    {
        double effective = EffectiveLevel(level);
        double dark = Math.Max(0.0, (500.0 - effective) / 500.0);
        double bright = Math.Max(0.0, (effective - 500.0) / 500.0);
        double gamma = 1.0 + dark * 1.20 - bright * 0.50;
        double gain = 1.0 - dark * 0.85 + bright * 0.25;
        double lift = bright * 0.08;
        return String.Format(System.Globalization.CultureInfo.InvariantCulture,
            "Level={0}, effective={1:F6}, gamma={2:F9}, gain={3:F9}, lift={4:F9}",
            level, effective, gamma, gain, lift);
    }

    private static byte[] BuildLut(int level)
    {
        double effective = EffectiveLevel(level);
        double dark = Math.Max(0.0, (500.0 - effective) / 500.0);
        double bright = Math.Max(0.0, (effective - 500.0) / 500.0);
        double gamma = 1.0 + dark * 1.20 - bright * 0.50;
        double gain = 1.0 - dark * 0.85 + bright * 0.25;
        double lift = bright * 0.08;

        byte[] lut = new byte[256];
        for (int i = 0; i < 256; ++i)
        {
            double x = (double)i / 255.0;
            double y = Math.Pow(x, gamma) * gain + lift;
            y = Math.Max(0.0, Math.Min(1.0, y));
            int v = (int)Math.Round(y * 255.0, MidpointRounding.ToEven);
            lut[i] = (byte)v;
        }
        return lut;
    }

    public static void RenderFile(string inputFile, string outputFile, int level)
    {
        if (String.Equals(Path.GetFullPath(inputFile), Path.GetFullPath(outputFile),
            StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Output cannot overwrite input.");

        byte[] lut = BuildLut(level);
        using (Image source = Image.FromFile(inputFile, false))
        using (Bitmap dst = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            // Convert to a writable 32bpp bitmap without resizing.
            // SourceCopy is important for preserving transparency instead of
            // compositing semi-transparent input onto a background.
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height),
                    0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
            }

            Rectangle rect = new Rectangle(0, 0, dst.Width, dst.Height);
            BitmapData data = dst.LockBits(rect, ImageLockMode.ReadWrite,
                PixelFormat.Format32bppArgb);
            try
            {
                // Format32bppArgb is BGRA bytes on little-endian Windows.
                // Operate only on B,G,R; leave alpha unchanged.
                byte[] row = new byte[dst.Width * 4];
                for (int y = 0; y < dst.Height; ++y)
                {
                    IntPtr rowPtr = IntPtr.Add(data.Scan0, y * data.Stride);
                    Marshal.Copy(rowPtr, row, 0, row.Length);
                    for (int x = 0; x < row.Length; x += 4)
                    {
                        row[x] = lut[row[x]];
                        row[x + 1] = lut[row[x + 1]];
                        row[x + 2] = lut[row[x + 2]];
                    }
                    Marshal.Copy(row, 0, rowPtr, row.Length);
                }
            }
            finally
            {
                dst.UnlockBits(data);
            }

            dst.Save(outputFile, ImageFormat.Png);
        }
    }
}
