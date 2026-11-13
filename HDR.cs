// ColorCurver high precision JPEG XR / linear scRGB path.
// Windows built-in WIC codec (through WPF) only. No external binary dependencies.
// This file is loaded only for .jxr images. Ordinary SDR processing stays unchanged.
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class HdrJxrEngine
{
    private static readonly Guid RgbaHalf =
        new Guid("6fddc324-4e03-4bfe-b185-3d77768dc93a");
    private static readonly Guid RgbaFloat =
        new Guid("6fddc324-4e03-4bfe-b185-3d77768dc919");

    // JPEG XR uses a TIFF-like little-endian container. Verify its declared format
    // before WPF has an opportunity to silently request a down-conversion.
    private static Guid ReadJxrPixelFormat(string path)
    {
        using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read,
                                                FileShare.Read))
        using (BinaryReader br = new BinaryReader(file))
        {
            if (file.Length < 32 || br.ReadByte() != 0x49 || br.ReadByte() != 0x49 ||
                br.ReadByte() != 0xBC || br.ReadByte() != 0x01)
                throw new InvalidDataException("Not a JPEG XR image container.");
            uint offset = br.ReadUInt32();
            if (offset < 8 || offset > file.Length - 2)
                throw new InvalidDataException("Invalid JPEG XR image directory.");
            file.Position = offset;
            ushort count = br.ReadUInt16();
            if (count < 1 || count > 4096 ||
                (long)offset + 2L + count * 12L > file.Length)
                throw new InvalidDataException("Corrupted JPEG XR image directory.");
            for (int entry = 0; entry < count; ++entry)
            {
                ushort tag = br.ReadUInt16();
                ushort type = br.ReadUInt16();
                uint length = br.ReadUInt32();
                uint dataOffset = br.ReadUInt32();
                if (tag == 0xBC01)
                {
                    if (type != 1 || length != 16 ||
                        dataOffset > file.Length - 16)
                        throw new InvalidDataException("Invalid JPEG XR pixel format header.");
                    file.Position = dataOffset;
                    byte[] bytes = br.ReadBytes(16);
                    if (bytes.Length != 16)
                        throw new EndOfStreamException("Truncated JPEG XR pixel format header.");
                    return new Guid(bytes);
                }
            }
        }
        throw new InvalidDataException("Missing JPEG XR pixel format tag.");
    }

    private static double EncodeSrgb(double linear)
    {
        if (linear <= 0.0031308) return 12.92 * linear;
        return 1.055 * Math.Pow(linear, 1.0 / 2.4) - 0.055;
    }

    private static double DecodeSrgb(double encoded)
    {
        if (encoded <= 0.04045) return encoded / 12.92;
        return Math.Pow((encoded + 0.055) / 1.055, 2.4);
    }

    // Fully preserve extended HDR channels >=1.0 and fade the SDR-range
    // brightening toward that boundary. The taper is eased early enough to
    // preserve highlight separation (consistent with the HDR-safe pilot).
    private static double HighlightProtection(double original)
    {
        if (original <= 0.25) return 1.0;
        if (original >= 1.0) return 0.0;
        double t = (original - 0.25) / 0.75;
        double remaining = 1.0 - t*t*(3.0-2.0*t);
        return Math.Pow(remaining, 1.3);
    }

    private static float MapComponent(double original, double lutResult)
    {
        if (original <= 0.0 || original >= 1.0) return (float)original;
        double mapped = DecodeSrgb(lutResult);
        double weight = HighlightProtection(original);
        return (float)(original + weight * (mapped - original));
    }

    // Input is straight-alpha scRGB RGBA in linear floating-point units.
    public static void MapPixel(float r, float g, float b, float alpha,
                                CubeLut lut,
                                out float rr, out float gg, out float bb, out float aa)
    {
        if (Single.IsNaN(r) || Single.IsNaN(g) || Single.IsNaN(b) ||
            Single.IsInfinity(r) || Single.IsInfinity(g) || Single.IsInfinity(b) ||
            Single.IsNaN(alpha) || Single.IsInfinity(alpha))
            throw new InvalidDataException("JXR contains non-finite RGB data.");

        double qr = r <= 0.0f ? 0.0 : r >= 1.0f ? 1.0 : EncodeSrgb(r);
        double qg = g <= 0.0f ? 0.0 : g >= 1.0f ? 1.0 : EncodeSrgb(g);
        double qb = b <= 0.0f ? 0.0 : b >= 1.0f ? 1.0 : EncodeSrgb(b);
        double lr, lg, lb;
        lut.ApplyContinuous(qr, qg, qb, out lr, out lg, out lb);
        rr = MapComponent(r, lr);
        gg = MapComponent(g, lg);
        bb = MapComponent(b, lb);
        aa = alpha; // Unchanged alpha (including partially transparent pixels).
    }

    private static void Progress(string phase, int index, int totalImages,
                                 int done, int total)
    {
        double imageRatio = total <= 0 ? 1.0 : (double)done / total;
        int percentage = (int)Math.Round(100.0 * imageRatio);
        int overall = (int)Math.Round(100.0 * ((index - 1) + imageRatio) / totalImages);
        Console.Write("\r" + String.Format(
            "{0} | Image {1}/{2} | Rows {3}/{4} | {5}% | Overall {6}%",
            phase, index, totalImages, done, total, percentage, overall).PadRight(118));
    }

    private static BitmapSource ToWorkingFloat(BitmapSource source)
    {
        if (source.Format == PixelFormats.Rgba128Float) return source;
        // WIC's native conversion keeps scRGB floating-point data; no 8-bit stage.
        return new FormatConvertedBitmap(source, PixelFormats.Rgba128Float, null, 0.0);
    }

    // Half->single conversion is exact for finite IEEE-754 half-float values.
    // This lets us detect an unintended color transform or HDR clamp by WPF.
    private static float HalfAsFloat(ushort bits)
    {
        int sign = (bits & 0x8000) != 0 ? -1 : 1;
        int e = (bits >> 10) & 0x1f;
        int mantissa = bits & 0x3ff;
        if (e == 31)
            throw new InvalidDataException("Non-finite FP16 pixels are unsupported.");
        if (e == 0) return (float)(sign * mantissa / 16777216.0);
        return (float)(sign * (1.0 + mantissa / 1024.0) * Math.Pow(2.0, e - 15));
    }

    private static void ValidateHalfToFloat(BitmapSource original, byte[] converted,
                                             int width, int height, int imageIndex,
                                             int imageCount)
    {
        int sourceStride = checked(width * 8);
        int destinationStride = checked(width * 16);
        byte[] bytes = new byte[sourceStride];
        float[] line = new float[checked(width * 4)];
        for (int y = 0; y < height; ++y)
        {
            original.CopyPixels(new Int32Rect(0, y, width, 1), bytes, sourceStride, 0);
            Buffer.BlockCopy(converted, checked(y * destinationStride), line, 0,
                             destinationStride);
            for (int i = 0; i < line.Length; ++i)
            {
                float expected = HalfAsFloat(BitConverter.ToUInt16(bytes, i * 2));
                if (expected != line[i])
                    throw new InvalidDataException("WIC/WPF HDR decoding is not bit-exact " +
                        "at row " + y + ", channel " + i + ". Processing stopped to protect HDR.");
            }
            if (y == 0 || (y + 1) % 128 == 0 || y == height - 1)
                Progress("Checking FP16 decode",imageIndex,imageCount,y+1,height);
        }
        Console.WriteLine();
    }

    private static void VerifyOutput(string outputFile, byte[] expected,
                                     int width, int height, int imageIndex, int imageCount)
    {
        Guid stored = ReadJxrPixelFormat(outputFile);
        if (stored != RgbaFloat)
            throw new InvalidDataException("The JXR codec changed the pixel format during encoding. " +
                "Output is not 128-bit floating-point RGBA; discarding unsafe file. " + stored);

        using (FileStream stream = new FileStream(outputFile, FileMode.Open, FileAccess.Read,
                                                  FileShare.Read))
        {
            BitmapDecoder decoder = BitmapDecoder.Create(stream,
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            BitmapSource frame = decoder.Frames[0];
            if (frame.PixelWidth != width || frame.PixelHeight != height)
                throw new InvalidDataException("Saved image dimensions have changed.");
            BitmapSource decoded = ToWorkingFloat(frame);
            int stride = checked(width * 16);
            byte[] row = new byte[stride];
            for (int y = 0; y < height; ++y)
            {
                decoded.CopyPixels(new Int32Rect(0, y, width, 1), row, stride, 0);
                int start = checked(y * stride);
                // JPEG XR's floating-point codec can round low float mantissa bits
                // even when Lossless=true. Enforce an absolute high-precision
                // error bound instead of a misleading 32-bit bitwise promise.
                for (int k = 0; k < stride; k += 4)
                {
                    float target = BitConverter.ToSingle(expected, start + k);
                    float actual = BitConverter.ToSingle(row, k);
                    if (Single.IsNaN(actual) || Single.IsInfinity(actual))
                        throw new InvalidDataException("Non-finite HDR output produced.");
                    double tolerance = 0.00005 + 0.00002 * Math.Abs((double)target);
                    if (Math.Abs((double)target - actual) > tolerance)
                        throw new InvalidDataException(
                            "HDR precision validation failed at row " + y +
                            ", channel " + (k / 4) + ". Output removed.");
                }
                if (y == 0 || (y + 1) % 128 == 0 || y == height - 1)
                    Progress("Verifying HDR precision", imageIndex, imageCount, y + 1, height);
            }
            Console.WriteLine();
        }
    }

    public static void RenderFile(string inputFile, string outputFile,
                                  CubeLut lut, int imageIndex, int imageCount)
    {
        if (lut == null) throw new ArgumentNullException("lut");
        if (String.Equals(Path.GetFullPath(inputFile), Path.GetFullPath(outputFile),
            StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Input and output files cannot be the same.");
        Guid inputFormat = ReadJxrPixelFormat(inputFile);
        if (inputFormat != RgbaHalf && inputFormat != RgbaFloat)
            throw new NotSupportedException("Only 64-bit RGBA half-float and " +
                "128-bit RGBA float JXR are supported. Input format is " + inputFormat);

        bool created = false;
        try
        {
            using (FileStream source = new FileStream(inputFile, FileMode.Open, FileAccess.Read,
                                                        FileShare.Read))
            {
                BitmapDecoder decoder = BitmapDecoder.Create(source,
                    BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count != 1)
                    throw new NotSupportedException("Only single-frame JPEG XR files are supported.");
                BitmapFrame frame = decoder.Frames[0];
                int width = frame.PixelWidth, height = frame.PixelHeight;
                if (width <= 0 || height <= 0)
                    throw new InvalidDataException("Invalid JXR dimensions.");
                int stride = checked(width * 16);
                byte[] pixels = new byte[checked(stride * height)];
                BitmapSource floatFrame = ToWorkingFloat(frame);
                floatFrame.CopyPixels(pixels, stride, 0);
                if (inputFormat == RgbaHalf && frame.Format.BitsPerPixel == 64)
                    ValidateHalfToFloat(frame, pixels, width, height, imageIndex, imageCount);

                float[] scanline = new float[checked(width * 4)];
                for (int y = 0; y < height; ++y)
                {
                    int start = checked(y * stride);
                    Buffer.BlockCopy(pixels, start, scanline, 0, stride);
                    for (int i = 0; i < scanline.Length; i += 4)
                    {
                        float r, g, b, a;
                        MapPixel(scanline[i], scanline[i + 1], scanline[i + 2],
                                 scanline[i + 3], lut, out r, out g, out b, out a);
                        scanline[i] = r;
                        scanline[i + 1] = g;
                        scanline[i + 2] = b;
                        scanline[i + 3] = a;
                    }
                    Buffer.BlockCopy(scanline, 0, pixels, start, stride);
                    if (y == 0 || (y + 1) % 32 == 0 || y == height - 1)
                        Progress("Mapping floating HDR", imageIndex, imageCount, y + 1, height);
                }
                Console.WriteLine();

                BitmapSource processed = BitmapSource.Create(width, height,
                    frame.DpiX > 0 ? frame.DpiX : 96,
                    frame.DpiY > 0 ? frame.DpiY : 96,
                    PixelFormats.Rgba128Float, null, pixels, stride);
                processed.Freeze();

                Console.WriteLine("Encoding high-precision floating-point JXR (lossless mode)...");
                WmpBitmapEncoder encoder = new WmpBitmapEncoder();
                encoder.Lossless = true;
                encoder.ImageQualityLevel = 1.0f;
                encoder.Frames.Add(BitmapFrame.Create(processed));
                using (FileStream output = new FileStream(outputFile, FileMode.CreateNew,
                                                           FileAccess.Write, FileShare.None))
                {
                    created = true;
                    encoder.Save(output);
                    output.Flush();
                }
                VerifyOutput(outputFile, pixels, width, height, imageIndex, imageCount);
                Progress("Done", imageIndex, imageCount, height, height);
                Console.WriteLine();
            }
        }
        catch
        {
            if (created) { try { File.Delete(outputFile); } catch { } }
            throw;
        }
    }
}
