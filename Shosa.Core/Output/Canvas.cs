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

    public uint ColorAt(int x, int y)
    {
        int i = (y * Width + x) * 3;
        return (uint)(_rgb[i] << 16 | _rgb[i + 1] << 8 | _rgb[i + 2]);
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

    /// <summary>A character is this many dots from the start of one to the start of the next, and a line this many high (7, and 1 for a tail: g j p q y).</summary>
    public const int GlyphAdvance = 6, GlyphHeight = 8;

    // The printable ASCII characters, ' ' to '~', as the classic 5x7 dot matrix: five columns
    // each, a byte per column in hex, the lowest bit the top row. Capitals and small letters are
    // different shapes, so a name reads as it was written (3x5 could not tell N from H, M or W).
    private const string Glyphs =
        "0000000000" + "00005F0000" + "0007000700" + "147F147F14" + "242A7F2A12" + "2313086462" + "3649562050" + "0008070300" +   //   ! " # $ % & '
        "001C224100" + "0041221C00" + "2A1C7F1C2A" + "08083E0808" + "0080703000" + "0808080808" + "0000606000" + "2010080402" +   // ( ) * + , - . /
        "3E5149453E" + "00427F4000" + "7249494946" + "2141494D33" + "1814127F10" + "2745454539" + "3C4A494931" + "4121110907" +   // 0 1 2 3 4 5 6 7
        "3649494936" + "464949291E" + "0000140000" + "0040340000" + "0008142241" + "1414141414" + "0041221408" + "0201590906" +   // 8 9 : ; < = > ?
        "3E415D594E" + "7C1211127C" + "7F49494936" + "3E41414122" + "7F4141413E" + "7F49494941" + "7F09090901" + "3E41415173" +   // @ A B C D E F G
        "7F0808087F" + "00417F4100" + "2040413F01" + "7F08142241" + "7F40404040" + "7F021C027F" + "7F0408107F" + "3E4141413E" +   // H I J K L M N O
        "7F09090906" + "3E4151215E" + "7F09192946" + "2649494932" + "03017F0103" + "3F4040403F" + "1F2040201F" + "3F4038403F" +   // P Q R S T U V W
        "6314081463" + "0304780403" + "6159494D43" + "007F414141" + "0204081020" + "004141417F" + "0402010204" + "4040404040" +   // X Y Z [ \ ] ^ _
        "0003070800" + "2054547840" + "7F28444438" + "3844444428" + "384444287F" + "3854545418" + "00087E0902" + "18A4A49C78" +   // ` a b c d e f g
        "7F08040478" + "00447D4000" + "2040403D00" + "7F10284400" + "00417F4000" + "7C04780478" + "7C08040478" + "3844444438" +   // h i j k l m n o
        "FC18242418" + "18242418FC" + "7C08040408" + "4854545424" + "04043F4424" + "3C4040207C" + "1C2040201C" + "3C4030403C" +   // p q r s t u v w
        "4428102844" + "4C9090907C" + "4464544C44" + "0008364100" + "0000770000" + "0041360800" + "0201020402";                    // x y z { | } ~

    /// <summary>How wide a text is drawn at a scale, without the gap after its last character.</summary>
    public static int TextWidth(string text, int scale) => Math.Max(0, text.Length * GlyphAdvance - 1) * scale;

    /// <summary>ASCII text; any other character is a gap. (x, y) is the top left of the first character.</summary>
    public void Text(int x, int y, string text, int scale, uint color)
    {
        foreach (char ch in text)
        {
            if (ch is > ' ' and <= '~')
            {
                for (int col = 0; col < 5; col++)
                {
                    int bits = Convert.ToInt32(Glyphs.Substring((ch - ' ') * 10 + col * 2, 2), 16);
                    for (int row = 0; row < GlyphHeight; row++)
                    {
                        if ((bits >> row & 1) != 0)
                            Rect(x + col * scale, y + row * scale, scale, scale, color);
                    }
                }
            }
            x += GlyphAdvance * scale;
        }
    }

    public void SavePng(string path) => File.WriteAllBytes(path, ToPng());

    /// <summary>The image as the bytes of a PNG file.</summary>
    public byte[] ToPng()
    {
        using var file = new MemoryStream();
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
        return file.ToArray();
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
