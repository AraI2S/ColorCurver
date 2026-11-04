// Generic 3D .cube LUT reader and image renderer for Windows PowerShell 5.1 / .NET Framework.
// No preset-specific color curves, brightness formulas, or application names are built in.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public sealed class CubeLut
{
    private readonly float[] data;
    private readonly int side;
    private readonly int stepG;
    private readonly int stepB;
    private readonly int[] indexR = new int[256];
    private readonly int[] indexG = new int[256];
    private readonly int[] indexB = new int[256];
    private readonly double[] fractionR = new double[256];
    private readonly double[] fractionG = new double[256];
    private readonly double[] fractionB = new double[256];

    public int Size { get { return side; } }

    private CubeLut(int n, float[] values, double[] min, double[] max)
    {
        side = n;
        stepG = n;
        stepB = n * n;
        data = values;
        BuildAxis(min[0], max[0], indexR, fractionR);
        BuildAxis(min[1], max[1], indexG, fractionG);
        BuildAxis(min[2], max[2], indexB, fractionB);
    }

    private void BuildAxis(double min, double max, int[] indices, double[] fractions)
    {
        for (int code = 0; code < 256; ++code)
        {
            double t = ((code / 255.0) - min) / (max - min) * (side - 1);
            if (t <= 0.0) { indices[code] = 0; fractions[code] = 0.0; }
            else if (t >= side - 1) { indices[code] = side - 2; fractions[code] = 1.0; }
            else
            {
                int index = (int)Math.Floor(t);
                indices[code] = index;
                fractions[code] = t - index;
            }
        }
    }

    private static string WithoutComment(string line)
    {
        bool inQuote = false;
        for (int i = 0; i < line.Length; ++i)
        {
            if (line[i] == '"') inQuote = !inQuote;
            else if (line[i] == '#' && !inQuote) return line.Substring(0, i);
        }
        return line;
    }

    private static double ReadNumber(string token, string location)
    {
        double v;
        if (!Double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ||
            Double.IsNaN(v) || Double.IsInfinity(v) || Math.Abs(v) > Single.MaxValue)
            throw new FormatException("Invalid finite number at " + location + ": " + token);
        return v;
    }

    private static string[] SplitWords(string line)
    {
        return line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    }

    public static CubeLut Load(string path)
    {
        int n = 0;
        int entries = 0;
        float[] values = null;
        int row = 0;
        int lineNumber = 0;
        double[] domainMin = new double[] { 0.0, 0.0, 0.0 };
        double[] domainMax = new double[] { 1.0, 1.0, 1.0 };
        bool minDefined = false;
        bool maxDefined = false;
        bool rangeDefined = false;
        bool dataStarted = false;
        using (StreamReader reader = new StreamReader(path, Encoding.UTF8, true))
        {
            string raw;
            while ((raw = reader.ReadLine()) != null)
            {
                ++lineNumber;
                string line = WithoutComment(raw).Trim();
                if (line.Length == 0) continue;
                string[] parts = SplitWords(line);
                string key = parts[0].ToUpperInvariant();
                string location = Path.GetFileName(path) + ":" + lineNumber;
                if (Char.IsLetter(parts[0][0]) || parts[0][0] == '_')
                {
                    if (dataStarted)
                        throw new FormatException("Header after LUT data at " + location);
                    switch (key)
                    {
                        case "TITLE":
                            // Metadata only; actual preset selection and output name use filename.
                            if (line.Length <= parts[0].Length)
                                throw new FormatException("Missing TITLE text at " + location);
                            break;
                        case "LUT_3D_SIZE":
                            if (n != 0 || parts.Length != 2 ||
                                !Int32.TryParse(parts[1], NumberStyles.Integer,
                                    CultureInfo.InvariantCulture, out n) || n < 2 || n > 256)
                                throw new FormatException("LUT_3D_SIZE must be an integer from 2 to 256 at " + location);
                            entries = checked(n * n * n);
                            values = new float[checked(entries * 3)];
                            break;
                        case "DOMAIN_MIN":
                        case "DOMAIN_MAX":
                            if (parts.Length != 4 || (key == "DOMAIN_MIN" && minDefined) ||
                                (key == "DOMAIN_MAX" && maxDefined) || rangeDefined)
                                throw new FormatException("Invalid or repeated " + key + " at " + location);
                            double[] target = key == "DOMAIN_MIN" ? domainMin : domainMax;
                            for (int i = 0; i < 3; ++i) target[i] = ReadNumber(parts[i + 1], location);
                            if (key == "DOMAIN_MIN") minDefined = true;
                            else maxDefined = true;
                            break;
                        case "LUT_3D_INPUT_RANGE":
                            // Common DaVinci Resolve dialect: a single pair applies to all channels.
                            if (parts.Length != 3 || minDefined || maxDefined || rangeDefined)
                                throw new FormatException("Invalid or conflicting LUT_3D_INPUT_RANGE at " + location);
                            double lower = ReadNumber(parts[1], location);
                            double upper = ReadNumber(parts[2], location);
                            for (int i = 0; i < 3; ++i) { domainMin[i] = lower; domainMax[i] = upper; }
                            rangeDefined = true;
                            break;
                        case "LUT_1D_SIZE":
                        case "LUT_1D_INPUT_RANGE":
                            throw new FormatException("This version accepts 3D .cube files only (no 1D shaper): " + location);
                        default:
                            throw new FormatException("Unsupported .cube header at " + location + ": " + parts[0]);
                    }
                }
                else
                {
                    if (n == 0)
                        throw new FormatException("Missing LUT_3D_SIZE before data at " + location);
                    dataStarted = true;
                    if (row >= entries || parts.Length != 3)
                        throw new FormatException("Expected exactly " + entries + " RGB data rows; invalid row at " + location);
                    int dst = row * 3;
                    for (int ch = 0; ch < 3; ++ch)
                        values[dst + ch] = (float)ReadNumber(parts[ch], location);
                    ++row;
                    if (row == 1 || row % 8192 == 0 || row == entries)
                    {
                        int percent = (int)(100.0 * row / entries);
                        Console.Write("\rLoading LUT | " + row + "/" + entries + " | " + percent + "%" + new string(' ', 30));
                    }
                }
            }
        }
        Console.WriteLine();
        if (n == 0) throw new FormatException("Missing LUT_3D_SIZE in " + Path.GetFileName(path));
        if (row != entries)
            throw new FormatException("Incomplete 3D LUT: expected " + entries + " rows, found " + row);
        for (int ch = 0; ch < 3; ++ch)
            if (domainMax[ch] <= domainMin[ch])
                throw new FormatException("DOMAIN_MAX must exceed DOMAIN_MIN in every channel.");
        return new CubeLut(n, values, domainMin, domainMax);
    }

    private static byte ToByte(double x)
    {
        if (x <= 0.0) return 0;
        if (x >= 1.0) return 255;
        return (byte)Math.Round(x * 255.0, MidpointRounding.ToEven);
    }

    public void Apply(byte red, byte green, byte blue,
                      out byte outRed, out byte outGreen, out byte outBlue)
    {
        int baseEntry = indexR[red] + stepG * indexG[green] + stepB * indexB[blue];
        double fr = fractionR[red];
        double fg = fractionG[green];
        double fb = fractionB[blue];

        // Tetrahedral interpolation: 4 corners of the cell rather than the 8 of trilinear.
        // Cube indexing follows Adobe's specification: R fastest, then G, then B.
        int p1, p2;
        double w1, w2, w3;
        if (fr >= fg)
        {
            if (fg >= fb)
            {
                p1 = 1; p2 = 1 + stepG; w1 = fr; w2 = fg; w3 = fb;
            }
            else if (fr >= fb)
            {
                p1 = 1; p2 = 1 + stepB; w1 = fr; w2 = fb; w3 = fg;
            }
            else
            {
                p1 = stepB; p2 = 1 + stepB; w1 = fb; w2 = fr; w3 = fg;
            }
        }
        else
        {
            if (fr >= fb)
            {
                p1 = stepG; p2 = 1 + stepG; w1 = fg; w2 = fr; w3 = fb;
            }
            else if (fg >= fb)
            {
                p1 = stepG; p2 = stepG + stepB; w1 = fg; w2 = fb; w3 = fr;
            }
            else
            {
                p1 = stepB; p2 = stepG + stepB; w1 = fb; w2 = fg; w3 = fr;
            }
        }

        int i0 = baseEntry * 3;
        int i1 = (baseEntry + p1) * 3;
        int i2 = (baseEntry + p2) * 3;
        int i3 = (baseEntry + 1 + stepG + stepB) * 3;
        double r0 = data[i0];
        double g0 = data[i0 + 1];
        double b0 = data[i0 + 2];
        double r1 = data[i1];
        double g1 = data[i1 + 1];
        double b1 = data[i1 + 2];
        double r2 = data[i2];
        double g2 = data[i2 + 1];
        double b2 = data[i2 + 2];
        double r3 = data[i3];
        double g3 = data[i3 + 1];
        double b3 = data[i3 + 2];

        outRed   = ToByte(r0 + (r1 - r0) * w1 + (r2 - r1) * w2 + (r3 - r2) * w3);
        outGreen = ToByte(g0 + (g1 - g0) * w1 + (g2 - g1) * w2 + (g3 - g2) * w3);
        outBlue  = ToByte(b0 + (b1 - b0) * w1 + (b2 - b1) * w2 + (b3 - b2) * w3);
    }
}

public static class ImageEngine
{
    private static void Report(string stage, int imageIndex, int imageCount,
                               int completed, int total)
    {
        int p = total <= 0 ? 100 : (int)(100.0 * completed / total);
        int overall = imageCount <= 0 ? 100 :
            (int)(100.0 * ((imageIndex - 1) + (total <= 0 ? 1.0 :
                          ((double)completed / total))) / imageCount);
        string message = String.Format(
            "{0} | Image {1}/{2} | Rows {3}/{4} | {5}% | Overall {6}%",
            stage, imageIndex, imageCount, completed, total, p, overall);
        Console.Write("\r" + message.PadRight(110));
    }

    public static void RenderFile(string inputFile, string outputFile,
                                  CubeLut lut, int imageIndex, int imageCount)
    {
        if (lut == null) throw new ArgumentNullException("lut");
        if (String.Equals(Path.GetFullPath(inputFile), Path.GetFullPath(outputFile),
            StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Input and output paths must differ.");

        using (Image source = Image.FromFile(inputFile, false))
        {
            if (source.Width < 1 || source.Height < 1)
                throw new ArgumentException("Invalid image dimensions.");

            using (Bitmap dst = new Bitmap(source.Width, source.Height,
                                           PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(dst))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height),
                        0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
                }

                Rectangle rect = new Rectangle(0, 0, dst.Width, dst.Height);
                BitmapData bitmap = dst.LockBits(rect, ImageLockMode.ReadWrite,
                                                PixelFormat.Format32bppArgb);
                try
                {
                    // Windows Format32bppArgb uses BGRA byte order.
                    byte[] row = new byte[dst.Width * 4];
                    for (int y = 0; y < dst.Height; ++y)
                    {
                        IntPtr rowPtr = IntPtr.Add(bitmap.Scan0, y * bitmap.Stride);
                        Marshal.Copy(rowPtr, row, 0, row.Length);
                        for (int x = 0; x < row.Length; x += 4)
                        {
                            byte r, g, b;
                            lut.Apply(row[x + 2], row[x + 1], row[x],
                                      out r, out g, out b);
                            row[x] = b;
                            row[x + 1] = g;
                            row[x + 2] = r;
                            // Preserve alpha channel at row[x + 3].
                        }
                        Marshal.Copy(row, 0, rowPtr, row.Length);
                        if (y == 0 || (y + 1) % 64 == 0 || y == dst.Height - 1)
                            Report("Mapping", imageIndex, imageCount, y + 1, dst.Height);
                    }
                }
                finally { dst.UnlockBits(bitmap); }

                Console.Write("\r" + String.Format(
                    "Saving PNG | Image {0}/{1} | Overall {2}%",
                    imageIndex, imageCount, (int)(100.0 * (imageIndex - 1) / imageCount))
                    .PadRight(110));

                // CreateNew guards against overwriting even if the output appeared meanwhile.
                bool created = false;
                try
                {
                    using (FileStream stream = new FileStream(outputFile, FileMode.CreateNew,
                                                              FileAccess.Write, FileShare.None))
                    {
                        created = true;
                        dst.Save(stream, ImageFormat.Png);
                    }
                }
                catch
                {
                    if (created) { try { File.Delete(outputFile); } catch { } }
                    throw;
                }
                Report("Done", imageIndex, imageCount, dst.Height, dst.Height);
                Console.WriteLine();
            }
        }
    }
}
