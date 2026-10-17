using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Xml.Linq;

namespace FormWind;

/// <summary>
/// Simple SVG icon renderer for single-color icon sets such as Lucide, Heroicons and Tabler. Supported:
/// <c>viewBox</c>, <c>path</c> (M L H V C S Q T A Z, relative/absolute), <c>circle</c>, <c>ellipse</c>, <c>rect</c> (rx/ry),
/// <c>line</c>, <c>polyline</c>, <c>polygon</c>, <c>g</c>; <c>fill</c>, <c>stroke</c>, <c>stroke-width</c>,
/// <c>stroke-linecap</c>, <c>stroke-linejoin</c> (on the element or inherited). Colors are ignored: the icon is painted in a single
/// color, the text color where it is drawn (like <c>currentColor</c>). Gradients, masks, text and CSS are not supported.
/// </summary>
internal sealed class TwSvgIcon
{
    private readonly List<Shape> _shapes = new List<Shape>();

    private TwSvgIcon(RectangleF viewBox)
    {
        ViewBox = viewBox;
    }

    public RectangleF ViewBox { get; }

    /// <summary>Number of shapes (for tests and diagnostics).</summary>
    public int ShapeCount => _shapes.Count;

    public static TwSvgIcon Parse(string svg)
    {
        if (string.IsNullOrWhiteSpace(svg)) throw new FormatException("The SVG is empty.");
        XElement root;
        try
        {
            root = XElement.Parse(svg);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new FormatException("Could not read the SVG: " + ex.Message, ex);
        }
        if (root.Name.LocalName != "svg") throw new FormatException("The root element is not <svg>.");

        var viewBox = ParseViewBox(root);
        var icon = new TwSvgIcon(viewBox);
        icon.Walk(root, Paint.Root);
        return icon;
    }

    /// <summary>Draws the icon in a square box of <paramref name="emPixels"/>, centered in <paramref name="bounds"/>.</summary>
    public void Draw(Graphics graphics, int emPixels, Rectangle bounds, Color color)
    {
        if (color.A == 0 || emPixels <= 0 || ViewBox.Width <= 0 || ViewBox.Height <= 0) return;
        float scale = emPixels / Math.Max(ViewBox.Width, ViewBox.Height);
        float x = bounds.X + (bounds.Width - ViewBox.Width * scale) / 2f;
        float y = bounds.Y + (bounds.Height - ViewBox.Height * scale) / 2f;

        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var matrix = new Matrix();
        matrix.Translate(x, y);
        matrix.Scale(scale, scale);
        matrix.Translate(-ViewBox.X, -ViewBox.Y);

        foreach (var shape in _shapes)
        {
            using var path = (GraphicsPath)shape.Path.Clone();
            path.Transform(matrix);
            if (shape.Paint.Fill)
            {
                using var brush = new SolidBrush(color);
                graphics.FillPath(brush, path);
            }
            if (shape.Paint.Stroke && shape.Paint.StrokeWidth > 0)
            {
                using var pen = new Pen(color, Math.Max(0.75f, shape.Paint.StrokeWidth * scale))
                {
                    StartCap = shape.Paint.LineCap,
                    EndCap = shape.Paint.LineCap,
                    LineJoin = shape.Paint.LineJoin,
                };
                graphics.DrawPath(pen, path);
            }
        }
        graphics.Restore(state);
    }

    private void Walk(XElement element, Paint inherited)
    {
        var paint = inherited.With(element);
        foreach (var child in element.Elements())
        {
            var childPaint = paint.With(child);
            var name = child.Name.LocalName;
            if (name == "g")
            {
                Walk(child, paint);
                continue;
            }
            if (name is "defs" or "title" or "desc" or "style" or "clipPath" or "mask") continue;

            var path = new GraphicsPath(FillMode.Winding);
            switch (name)
            {
                case "path":
                    var data = (string?)child.Attribute("d");
                    if (data != null) PathData.Append(path, data);
                    if (((string?)child.Attribute("fill-rule") ?? "") == "evenodd") path.FillMode = FillMode.Alternate;
                    break;
                case "circle":
                    float cx = Num(child, "cx"), cy = Num(child, "cy"), r = Num(child, "r");
                    if (r > 0) path.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                    break;
                case "ellipse":
                    float ex = Num(child, "cx"), ey = Num(child, "cy"), rx = Num(child, "rx"), ry = Num(child, "ry");
                    if (rx > 0 && ry > 0) path.AddEllipse(ex - rx, ey - ry, rx * 2, ry * 2);
                    break;
                case "rect":
                    AddRect(path, child);
                    break;
                case "line":
                    path.AddLine(Num(child, "x1"), Num(child, "y1"), Num(child, "x2"), Num(child, "y2"));
                    break;
                case "polyline":
                case "polygon":
                    var points = ParsePoints((string?)child.Attribute("points") ?? "");
                    if (points.Length >= 2)
                    {
                        path.AddLines(points);
                        if (name == "polygon") path.CloseFigure();
                    }
                    break;
                default:
                    path.Dispose();
                    continue;
            }
            if (path.PointCount == 0)
            {
                path.Dispose();
                continue;
            }
            _shapes.Add(new Shape(path, childPaint));
        }
    }

    private static void AddRect(GraphicsPath path, XElement element)
    {
        float x = Num(element, "x"), y = Num(element, "y"), w = Num(element, "width"), h = Num(element, "height");
        if (w <= 0 || h <= 0) return;
        float rx = Num(element, "rx", -1), ry = Num(element, "ry", -1);
        if (rx < 0) rx = ry;
        if (ry < 0) ry = rx;
        rx = Math.Min(Math.Max(0, rx), w / 2);
        ry = Math.Min(Math.Max(0, ry), h / 2);
        if (rx <= 0 || ry <= 0)
        {
            path.AddRectangle(new RectangleF(x, y, w, h));
            return;
        }
        path.StartFigure();
        path.AddArc(x, y, rx * 2, ry * 2, 180, 90);
        path.AddArc(x + w - rx * 2, y, rx * 2, ry * 2, 270, 90);
        path.AddArc(x + w - rx * 2, y + h - ry * 2, rx * 2, ry * 2, 0, 90);
        path.AddArc(x, y + h - ry * 2, rx * 2, ry * 2, 90, 90);
        path.CloseFigure();
    }

    private static RectangleF ParseViewBox(XElement root)
    {
        var text = (string?)root.Attribute("viewBox");
        if (text != null)
        {
            var parts = Numbers(text);
            if (parts.Count == 4 && parts[2] > 0 && parts[3] > 0) return new RectangleF(parts[0], parts[1], parts[2], parts[3]);
        }
        float width = Num(root, "width", 24), height = Num(root, "height", 24);
        return new RectangleF(0, 0, width, height);
    }

    private static PointF[] ParsePoints(string text)
    {
        var numbers = Numbers(text);
        var points = new PointF[numbers.Count / 2];
        for (int i = 0; i < points.Length; i++) points[i] = new PointF(numbers[i * 2], numbers[i * 2 + 1]);
        return points;
    }

    private static float Num(XElement element, string name, float fallback = 0)
    {
        var text = (string?)element.Attribute(name);
        if (text is null) return fallback;
        text = text.Trim();
        if (text.EndsWith("px", StringComparison.Ordinal)) text = text.Substring(0, text.Length - 2);
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    internal static List<float> Numbers(string text)
    {
        var result = new List<float>();
        var reader = new PathData.Reader(text);
        while (reader.TryNumber(out var value)) result.Add(value);
        return result;
    }

    private sealed class Shape
    {
        public Shape(GraphicsPath path, Paint paint)
        {
            Path = path;
            Paint = paint;
        }

        public GraphicsPath Path { get; }

        public Paint Paint { get; }
    }

    /// <summary>Inherited paint properties. SVG default: black fill, no stroke.</summary>
    private sealed class Paint
    {
        public static readonly Paint Root = new Paint();

        public bool Fill { get; private set; } = true;
        public bool Stroke { get; private set; }
        public float StrokeWidth { get; private set; } = 1;
        public LineCap LineCap { get; private set; } = LineCap.Flat;
        public LineJoin LineJoin { get; private set; } = LineJoin.Miter;

        public Paint With(XElement element)
        {
            var fill = Attr(element, "fill");
            var stroke = Attr(element, "stroke");
            var width = Attr(element, "stroke-width");
            var cap = Attr(element, "stroke-linecap");
            var join = Attr(element, "stroke-linejoin");
            if (fill is null && stroke is null && width is null && cap is null && join is null) return this;

            var paint = (Paint)MemberwiseClone();
            if (fill != null) paint.Fill = fill != "none" && fill != "transparent";
            if (stroke != null) paint.Stroke = stroke != "none" && stroke != "transparent";
            if (width != null && float.TryParse(width.Replace("px", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var w)) paint.StrokeWidth = w;
            if (cap != null) paint.LineCap = cap switch { "round" => LineCap.Round, "square" => LineCap.Square, _ => LineCap.Flat };
            if (join != null) paint.LineJoin = join switch { "round" => LineJoin.Round, "bevel" => LineJoin.Bevel, _ => LineJoin.Miter };
            return paint;
        }

        // Öznitelik ya da style="fill:none; stroke-width:2" içinden.
        private static string? Attr(XElement element, string name)
        {
            var value = (string?)element.Attribute(name);
            var style = (string?)element.Attribute("style");
            if (style != null)
            {
                foreach (var declaration in style.Split(';'))
                {
                    int colon = declaration.IndexOf(':');
                    if (colon > 0 && declaration.Substring(0, colon).Trim() == name) value = declaration.Substring(colon + 1).Trim();
                }
            }
            return value?.Trim();
        }
    }

    /// <summary>SVG path "d" syntax.</summary>
    internal static class PathData
    {
        public static void Append(GraphicsPath path, string data)
        {
            var reader = new Reader(data);
            PointF current = PointF.Empty, start = PointF.Empty, lastControl = PointF.Empty;
            char command = ' ', previous = ' ';
            bool figure = false;

            while (true)
            {
                if (reader.TryCommand(out var next)) command = next;
                else if (command == ' ' || !reader.HasNumber) break;
                else if (command == 'M') command = 'L';          // M'den sonraki koordinatlar örtük L'dir
                else if (command == 'm') command = 'l';

                bool relative = char.IsLower(command);
                float ox = relative ? current.X : 0, oy = relative ? current.Y : 0;
                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                        current = new PointF(ox + reader.Number(), oy + reader.Number());
                        start = current;
                        path.StartFigure();
                        figure = true;
                        break;
                    case 'L':
                    {
                        var to = new PointF(ox + reader.Number(), oy + reader.Number());
                        path.AddLine(current, to);
                        current = to;
                        break;
                    }
                    case 'H':
                    {
                        var to = new PointF((relative ? current.X : 0) + reader.Number(), current.Y);
                        path.AddLine(current, to);
                        current = to;
                        break;
                    }
                    case 'V':
                    {
                        var to = new PointF(current.X, (relative ? current.Y : 0) + reader.Number());
                        path.AddLine(current, to);
                        current = to;
                        break;
                    }
                    case 'C':
                    {
                        var c1 = new PointF(ox + reader.Number(), oy + reader.Number());
                        var c2 = new PointF(ox + reader.Number(), oy + reader.Number());
                        var to = new PointF(ox + reader.Number(), oy + reader.Number());
                        path.AddBezier(current, c1, c2, to);
                        lastControl = c2;
                        current = to;
                        break;
                    }
                    case 'S':
                    {
                        var c1 = "CcSs".IndexOf(previous) >= 0 ? Reflect(lastControl, current) : current;
                        var c2 = new PointF(ox + reader.Number(), oy + reader.Number());
                        var to = new PointF(ox + reader.Number(), oy + reader.Number());
                        path.AddBezier(current, c1, c2, to);
                        lastControl = c2;
                        current = to;
                        break;
                    }
                    case 'Q':
                    {
                        var q = new PointF(ox + reader.Number(), oy + reader.Number());
                        var to = new PointF(ox + reader.Number(), oy + reader.Number());
                        AddQuadratic(path, current, q, to);
                        lastControl = q;
                        current = to;
                        break;
                    }
                    case 'T':
                    {
                        var q = "QqTt".IndexOf(previous) >= 0 ? Reflect(lastControl, current) : current;
                        var to = new PointF(ox + reader.Number(), oy + reader.Number());
                        AddQuadratic(path, current, q, to);
                        lastControl = q;
                        current = to;
                        break;
                    }
                    case 'A':
                    {
                        float rx = reader.Number(), ry = reader.Number(), rotation = reader.Number();
                        bool large = reader.Flag(), sweep = reader.Flag();
                        var to = new PointF(ox + reader.Number(), oy + reader.Number());
                        AddArc(path, current, rx, ry, rotation, large, sweep, to);
                        current = to;
                        break;
                    }
                    case 'Z':
                        if (figure) path.CloseFigure();
                        current = start;
                        break;
                    default:
                        return; // bilinmeyen komut: kalanı atla
                }
                previous = command;
                if (char.ToUpperInvariant(command) == 'Z') command = ' ';
            }
        }

        private static PointF Reflect(PointF control, PointF around) => new PointF(2 * around.X - control.X, 2 * around.Y - control.Y);

        private static void AddQuadratic(GraphicsPath path, PointF from, PointF control, PointF to)
        {
            var c1 = new PointF(from.X + 2f / 3f * (control.X - from.X), from.Y + 2f / 3f * (control.Y - from.Y));
            var c2 = new PointF(to.X + 2f / 3f * (control.X - to.X), to.Y + 2f / 3f * (control.Y - to.Y));
            path.AddBezier(from, c1, c2, to);
        }

        // SVG eliptik yay → kübik Bézier parçaları (W3C SVG ek F.6 uç nokta → merkez dönüşümü).
        private static void AddArc(GraphicsPath path, PointF from, float rx, float ry, float rotationDegrees, bool large, bool sweep, PointF to)
        {
            if (from == to) return;
            rx = Math.Abs(rx);
            ry = Math.Abs(ry);
            if (rx == 0 || ry == 0)
            {
                path.AddLine(from, to);
                return;
            }

            double phi = rotationDegrees * Math.PI / 180, cos = Math.Cos(phi), sin = Math.Sin(phi);
            double dx = (from.X - to.X) / 2.0, dy = (from.Y - to.Y) / 2.0;
            double x1 = cos * dx + sin * dy, y1 = -sin * dx + cos * dy;
            double lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lambda > 1)
            {
                rx *= (float)Math.Sqrt(lambda);
                ry *= (float)Math.Sqrt(lambda);
            }

            double num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            double den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            double coef = (large == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0, num / den));
            double cxp = coef * rx * y1 / ry, cyp = -coef * ry * x1 / rx;
            double cx = cos * cxp - sin * cyp + (from.X + to.X) / 2.0;
            double cy = sin * cxp + cos * cyp + (from.Y + to.Y) / 2.0;

            double Angle(double ux, double uy, double vx, double vy)
            {
                double a = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
                return a;
            }

            double theta = Angle(1, 0, (x1 - cxp) / rx, (y1 - cyp) / ry);
            double delta = Angle((x1 - cxp) / rx, (y1 - cyp) / ry, (-x1 - cxp) / rx, (-y1 - cyp) / ry);
            if (!sweep && delta > 0) delta -= 2 * Math.PI;
            else if (sweep && delta < 0) delta += 2 * Math.PI;

            int segments = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2)));
            double step = delta / segments;
            double t = 4.0 / 3.0 * Math.Tan(step / 4);

            PointF Point(double angle, double scaleX, double scaleY) => new PointF(
                (float)(cx + cos * rx * scaleX - sin * ry * scaleY),
                (float)(cy + sin * rx * scaleX + cos * ry * scaleY));

            var currentPoint = from;
            for (int i = 0; i < segments; i++)
            {
                double a1 = theta + i * step, a2 = a1 + step;
                double c1 = Math.Cos(a1), s1 = Math.Sin(a1), c2 = Math.Cos(a2), s2 = Math.Sin(a2);
                var p1 = Point(0, c1 - t * s1, s1 + t * c1);
                var p2 = Point(0, c2 + t * s2, s2 - t * c2);
                var p3 = i == segments - 1 ? to : Point(0, c2, s2);
                path.AddBezier(currentPoint, p1, p2, p3);
                currentPoint = p3;
            }
        }

        /// <summary>Number and command reader: also parses forms without separators such as "M10-5.5.5".</summary>
        internal sealed class Reader
        {
            private readonly string _text;
            private int _position;

            public Reader(string text) => _text = text ?? "";

            private void SkipSeparators()
            {
                while (_position < _text.Length && (char.IsWhiteSpace(_text[_position]) || _text[_position] == ',')) _position++;
            }

            public bool HasNumber
            {
                get
                {
                    SkipSeparators();
                    if (_position >= _text.Length) return false;
                    char c = _text[_position];
                    return char.IsDigit(c) || c is '-' or '+' or '.';
                }
            }

            public bool TryCommand(out char command)
            {
                SkipSeparators();
                command = ' ';
                if (_position >= _text.Length) return false;
                char c = _text[_position];
                if ("MmLlHhVvCcSsQqTtAaZz".IndexOf(c) < 0) return false;
                command = c;
                _position++;
                return true;
            }

            public float Number() => TryNumber(out var value) ? value : 0;

            /// <summary>Arc flag: "0"/"1"; may be written without separators, as in "a1 1 0 011 1".</summary>
            public bool Flag()
            {
                SkipSeparators();
                if (_position < _text.Length && (_text[_position] == '0' || _text[_position] == '1'))
                    return _text[_position++] == '1';
                return false;
            }

            public bool TryNumber(out float value)
            {
                value = 0;
                SkipSeparators();
                int start = _position;
                if (_position < _text.Length && (_text[_position] == '-' || _text[_position] == '+')) _position++;
                bool digits = false, dot = false;
                while (_position < _text.Length)
                {
                    char c = _text[_position];
                    if (char.IsDigit(c)) digits = true;
                    else if (c == '.' && !dot) dot = true;
                    else break;
                    _position++;
                }
                if (digits && _position < _text.Length && (_text[_position] == 'e' || _text[_position] == 'E'))
                {
                    int save = _position++;
                    if (_position < _text.Length && (_text[_position] == '-' || _text[_position] == '+')) _position++;
                    if (_position < _text.Length && char.IsDigit(_text[_position]))
                        while (_position < _text.Length && char.IsDigit(_text[_position])) _position++;
                    else _position = save;
                }
                if (!digits)
                {
                    _position = start;
                    return false;
                }
                return float.TryParse(_text.Substring(start, _position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            }
        }
    }
}
