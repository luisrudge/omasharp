// Scrolling area graphs for DrawView: newest sample at the right edge, a filled area under a
// line through the samples (Drawing.Area, then Drawing.Polyline through the same points).
using Omasharp;

static class Graph
{
    /// <summary>Faint horizontal guides at a quarter, half and three quarters of the height.</summary>
    public static void Grid(Drawing d, double w, double h, Color color)
    {
        for (var i = 1; i < 4; i++) d.Line(0, Math.Round(h * i / 4) + 0.5, w, Math.Round(h * i / 4) + 0.5, 1, color);
    }

    /// <summary>`values` oldest first; `capacity` samples span the full width.</summary>
    public static void Area(Drawing d, IReadOnlyList<double> values, int capacity, double max, double w, double h, Color color, double fill)
    {
        var n = values.Count;
        if (n < 2 || w <= 0 || h <= 0 || max <= 0) return;
        var step = w / (capacity - 1);
        var start = w - (n - 1) * step;

        Span<(double X, double Y)> points = stackalloc (double, double)[n];
        for (var i = 0; i < n; i++)
            points[i] = (start + i * step, h - 1 - Math.Clamp(values[i] / max, 0, 1) * (h - 2));
        d.Area(points, h, color.WithAlpha(fill));
        d.Polyline(points, 2, color);
    }

    /// <summary>A small caption in the top-left corner (the scale).</summary>
    public static void Caption(Drawing d, string text, Color color) => d.Text(6, 10, text, 11, color);
}
