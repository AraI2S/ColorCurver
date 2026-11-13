// Small independent report helper. No inverse mathematics and no LUT implementation.
// Both images have already been rendered by the unchanged ImageEngine in Library.cs.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class ImageComparison
{
    private static Bitmap ToArgb(Image src)
    {
        Bitmap dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(dst))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height),
                        0, 0, src.Width, src.Height, GraphicsUnit.Pixel);
        }
        return dst;
    }

    public static string CompareAndReport(string desiredFile, string replayFile,
                                          string reportFile, string virtualFile,
                                          string forwardName, string inverseName)
    {
        double absolute = 0.0, squared = 0.0, pixelWeight = 0.0, near = 0.0;
        int width, height;
        using (Image desiredSrc = Image.FromFile(desiredFile, false))
        using (Image replaySrc = Image.FromFile(replayFile, false))
        {
            if (desiredSrc.Width != replaySrc.Width || desiredSrc.Height != replaySrc.Height)
                throw new ArgumentException("Desired and replay images have different dimensions.");
            using (Bitmap desired = ToArgb(desiredSrc))
            using (Bitmap replay = ToArgb(replaySrc))
            {
                width = desired.Width; height = desired.Height;
                Rectangle rect = new Rectangle(0, 0, width, height);
                BitmapData a = null, b = null;
                try
                {
                    a = desired.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    b = replay.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    byte[] rowA = new byte[width * 4], rowB = new byte[width * 4];
                    for (int y = 0; y < height; ++y)
                    {
                        Marshal.Copy(IntPtr.Add(a.Scan0, y * a.Stride), rowA, 0, rowA.Length);
                        Marshal.Copy(IntPtr.Add(b.Scan0, y * b.Stride), rowB, 0, rowB.Length);
                        for (int i = 0; i < rowA.Length; i += 4)
                        {
                            byte alpha = rowA[i + 3];
                            if (alpha == 0) continue;
                            double w = alpha / 255.0;
                            int db = Math.Abs((int)rowA[i] - rowB[i]);
                            int dg = Math.Abs((int)rowA[i + 1] - rowB[i + 1]);
                            int dr = Math.Abs((int)rowA[i + 2] - rowB[i + 2]);
                            absolute += w * (dr + dg + db);
                            squared += w * (dr * dr + dg * dg + db * db);
                            pixelWeight += w;
                            if (dr <= 3 && dg <= 3 && db <= 3) near += w;
                        }
                    }
                }
                finally
                {
                    if (b != null) replay.UnlockBits(b);
                    if (a != null) desired.UnlockBits(a);
                }
            }
        }
        if (pixelWeight <= 0.0) throw new InvalidOperationException("No visible pixels.");
        double mae = absolute / (3.0 * pixelWeight);
        double rmse = Math.Sqrt(squared / (3.0 * pixelWeight));
        double within = near * 100.0 / pixelWeight;
        string summary = String.Format(CultureInfo.InvariantCulture,
            "RGB MAE {0:F4}/255 | RMSE {1:F4} | Within +/-3 on all channels {2:F2}%",
            mae, rmse, within);
        string report =
            "3D LUT reverse preview - digital replay comparison\r\n" +
            "Desired image: " + desiredFile + "\r\n" +
            "Virtual original: " + virtualFile + "\r\n" +
            "Forward replay: " + replayFile + "\r\n" +
            "Forward LUT: " + forwardName + "\r\n" +
            "Inverse LUT: " + inverseName + "\r\n" +
            "Image dimensions: " + width + " x " + height + "\r\n" +
            summary + "\r\n" +
            "Metrics compare 8-bit RGB; fully transparent pixels are ignored; others are alpha-weighted.\r\n" +
            "Some desired colors cannot be reproduced by clipped, non-injective, or gamut-limited LUTs.\r\n" +
            "This measures only the digital round trip of the selected LUT pair; it does not validate any external physical process.\r\n";
        bool created = false;
        try
        {
            using (FileStream stream = new FileStream(reportFile, FileMode.CreateNew,
                                                      FileAccess.Write, FileShare.None))
            {
                created = true;
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    writer.Write(report);
            }
        }
        catch
        {
            if (created) { try { File.Delete(reportFile); } catch { } }
            throw;
        }
        return summary;
    }
}
