using System.IO.Compression;

namespace Shosa.Core.Output;

/// <summary>
/// A small RGB raster with just enough drawing to show a stick figure, and a PNG writer.
/// No dependencies: the contact sheet is how an author who cannot watch a video (an AI,
/// a test) sees the movement, so it must work wherever the compiler does.
/// </summary>
public sealed class Canvas
{
    private readonly byte[] _rgb;

    public Canvas(int width, int height)
    {
        Width = width;
        Height = height;
        _rgb = new byte[width * height * 3];
        Array.Fill(_rgb, (byte)255);
    }

    public int Width { get; }
    public int Height { get; }

    public void Pixel(int x, int y, uint color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return;
        int i = (y * Width + x) * 3;
        _rgb[i] = (byte)(color >> 16);
        _rgb[i + 1] = (byte)(color >> 8);
        _rgb[i + 2] = (byte)color;
    }

    public void Rect(int x, int y, int width, int height, uint color)
    {
        for (int yy = y; yy < y + height; yy++)
        {
            for (int xx = x; xx < x + width; xx++)
                Pixel(xx, yy, color);
        }
    }

    public void Disc(double cx, double cy, double radius, uint color)
    {
        int r = (int)Math.Ceiling(radius);
        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy <= radius * radius + 0.5)
                    Pixel((int)Math.Round(cx) + dx, (int)Math.Round(cy) + dy, color);
            }
        }
    }

    public void Ring(double cx, double cy, double radius, double thickness, uint color)
    {
        int steps = Math.Max(24, (int)(radius * 8));
        for (int i = 0; i < steps; i++)
        {
            double a = 2 * Math.PI * i / steps;
            Disc(cx + Math.Cos(a) * radius, cy + Math.Sin(a) * radius, thickness / 2, color);
        }
    }

    public void Line(double x0, double y0, double x1, double y1, double thickness, uint color)
    {
        double length = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        int steps = Math.Max(1, (int)(length * 2));
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            Disc(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, thickness / 2, color);
        }
    }

    // 3x5 glyphs, one bit per pixel, rows top to bottom.
    private static readonly Dictionary<char, string> Glyphs = new()
    {
        ['0'] = "111101101101111", ['1'] = "010110010010111", ['2'] = "111001111100111", ['3'] = "111001111001111",
        ['4'] = "101101111001001", ['5'] = "111100111001111", ['6'] = "111100111101111", ['7'] = "111001001001001",
        ['8'] = "111101111101111", ['9'] = "111101111001111", ['.'] = "000000000000010", ['-'] = "000000111000000",
        [' '] = "000000000000000",
    };

    /// <summary>Digits, '.', '-' and space, enough for beat labels.</summary>
    public void Text(int x, int y, string text, int scale, uint color)
    {
        foreach (char ch in text)
        {
            if (Glyphs.TryGetValue(ch, out var glyph))
            {
                for (int row = 0; row < 5; row++)
                {
                    for (int col = 0; col < 3; col++)
                    {
                        if (glyph[row * 3 + col] == '1')
                            Rect(x + col * scale, y + row * scale, scale, scale, color);
                    }
                }
            }
            x += 4 * scale;
        }
    }

    public void SavePng(string path)
    {
        using var file = File.Create(path);
        file.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        WriteInt(header, 0, Width);
        WriteInt(header, 4, Height);
        header[8] = 8;   // bit depth
        header[9] = 2;   // colour type: RGB
        Chunk(file, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (int y = 0; y < Height; y++)
            {
                zlib.WriteByte(0);   // filter: none
                zlib.Write(_rgb, y * Width * 3, Width * 3);
            }
        }
        Chunk(file, "IDAT", compressed.ToArray());
        Chunk(file, "IEND", []);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static readonly uint[] CrcTable = MakeCrcTable();

    private static uint[] MakeCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        stream.Write(length);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        uint crc = 0xFFFFFFFF;
        foreach (byte b in typeBytes)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (byte b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        crc ^= 0xFFFFFFFF;

        var crcBytes = new byte[4];
        WriteInt(crcBytes, 0, (int)crc);
        stream.Write(crcBytes);
    }
}
