using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Omasharp;

public sealed class Drawing
{
    readonly ArrayBufferWriter<byte> _buf = new(1024);
    int _count;

    public static double MonoAdvance { get; internal set; } = 0.6;

    public Drawing(double viewScale = 1, double viewX = 0, double viewY = 0)
    {
        _buf.Write("ODL1"u8);
        D(viewScale);
        D(viewX);
        D(viewY);
        I(0);
    }

    public int Count => _count;

    public void Rect(double x, double y, double w, double h, Color color)
    {
        if (w <= 0 || h <= 0 || color.A == 0) return;
        B(1); F(x); F(y); F(w); F(h); U(color.Argb); _count++;
    }

    public void Frame(double x, double y, double w, double h, double width, Color color)
    {
        if (w <= 0 || h <= 0 || color.A == 0) return;
        B(2); F(x); F(y); F(w); F(h); F(width); U(color.Argb); _count++;
    }

    public void Hatch(double x, double y, double w, double h, double spacing, Color color, double thickness = 1)
    {
        if (w <= 0 || h <= 0 || color.A == 0) return;
        B(3); F(x); F(y); F(w); F(h); F(spacing); F(thickness); U(color.Argb); _count++;
    }

    public void Line(double x1, double y1, double x2, double y2, double width, Color color) =>
        Polyline([(x1, y1), (x2, y2)], width, color);

    public void Polyline(ReadOnlySpan<(double X, double Y)> points, double width, Color color)
    {
        if (points.Length < 2 || width <= 0 || color.A == 0) return;
        B(5); F(width); U(color.Argb); Points(points); _count++;
    }

    public void Area(ReadOnlySpan<(double X, double Y)> points, double baselineY, Color color)
    {
        if (points.Length < 2 || color.A == 0) return;
        B(6); F(baselineY); U(color.Argb); Points(points); _count++;
    }

    public void Text(double x, double yCenter, string text, double pixelSize, Color color, bool bold = false, double maxWidth = 0)
    {
        if (string.IsNullOrEmpty(text) || color.A == 0) return;
        B(4); F(x); F(yCenter); F(pixelSize); F(maxWidth); U(color.Argb);
        B(bold ? (byte)1 : (byte)0);
        var bytes = Encoding.UTF8.GetBytes(text);
        I(bytes.Length);
        _buf.Write(bytes);
        _count++;
    }

    void Points(ReadOnlySpan<(double X, double Y)> points)
    {
        I(points.Length);
        foreach (var (x, y) in points) { F(x); F(y); }
    }
    void B(byte v) { _buf.GetSpan(1)[0] = v; _buf.Advance(1); }
    void I(int v) { BinaryPrimitives.WriteInt32LittleEndian(_buf.GetSpan(4), v); _buf.Advance(4); }
    void U(uint v) { BinaryPrimitives.WriteUInt32LittleEndian(_buf.GetSpan(4), v); _buf.Advance(4); }
    void F(double v) { BinaryPrimitives.WriteSingleLittleEndian(_buf.GetSpan(4), (float)v); _buf.Advance(4); }
    void D(double v) { BinaryPrimitives.WriteDoubleLittleEndian(_buf.GetSpan(8), v); _buf.Advance(8); }

    internal byte[] ToBytes()
    {
        var bytes = _buf.WrittenSpan.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + 3 * 8, 4), _count);
        return bytes;
    }
}

public readonly record struct Color(uint Argb)
{
    public byte A => (byte)(Argb >> 24);

    public static Color Parse(string css)
    {
        var hex = css.AsSpan().TrimStart('#');
        if (hex.Length == 3) hex = new string([hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]]);
        var v = uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Color(hex.Length == 6 ? 0xFF000000 | v : v);
    }

    public Color WithAlpha(double alpha) => new(((uint)Math.Clamp(alpha * 255, 0, 255) << 24) | (Argb & 0xFFFFFF));
}
