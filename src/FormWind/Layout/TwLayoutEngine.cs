using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Layout;

namespace FormWind;

/// <summary>
/// A subset of CSS flexbox and grid. Lays out children in <see cref="Control.Controls"/> order inside the container's
/// <see cref="Control.DisplayRectangle"/> (padding excluded). Dock and Anchor are ignored.
/// </summary>
internal sealed class TwLayoutEngine : LayoutEngine
{
    public static readonly TwLayoutEngine Instance = new TwLayoutEngine();

    private const float Infinity = float.PositiveInfinity;

    // Sağdan sola konteynerde (RightToLeft = Yes) yerleşim yatayda aynalanır: X, alanın sol ve sağ kenarlarının toplamından
    // öğenin sağ kenarı çıkarılarak bulunur. İç içe yerleşimler kendi değerini kurar ve geri yükler.
    [ThreadStatic] private static int _mirrorAxis;
    [ThreadStatic] private static bool _mirror;

    public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
    {
        if (container is not Control parent) return false;
        if (TwLayout.Find(parent)?.Container is not TwContainerProps props) return false;

        var entries = Collect(parent);
        var area = parent.DisplayRectangle;

        // AutoScroll konteynerde DisplayRectangle önceki içerik genişliğini korur; dikey kaydırma çubuğu belirince
        // öğeler eski genişlikte kalıp yatay çubuk çıkarırdı. Genişlik her zaman görünen alandan alınır.
        if (parent is ScrollableControl { AutoScroll: true } scrollable)
        {
            var visible = scrollable.ClientRectangle.Width - scrollable.Padding.Horizontal;
            if (parent is TwPanel panel) visible -= panel.BorderInsets().Horizontal;
            area = new Rectangle(area.X, area.Y, Math.Max(0, visible), area.Height);
        }

        var (savedMirror, savedAxis) = (_mirror, _mirrorAxis);
        _mirror = TwRtl.NeedsMirroring(parent);
        _mirrorAxis = area.Left + area.Right;
        TwLayout.Enter();
        try
        {
            Run(area, props, entries);

            // Dikey kaydırma çubuğu yerleşimden sonra belirip görünen alanı daraltır (ve yatay çubuk çıkarır);
            // içerik taşıyorsa çubuğun yeri baştan ayrılıp yerleşim bir kez daha yapılır.
            if (parent is ScrollableControl { AutoScroll: true } scroll && !scroll.VerticalScroll.Visible
                && ContentBottom(entries) - area.Y > scroll.ClientRectangle.Height)
            {
                area.Width = Math.Max(0, area.Width - SystemInformation.VerticalScrollBarWidth);
                Run(area, props, entries);
            }
        }
        finally
        {
            TwLayout.Exit();
            (_mirror, _mirrorAxis) = (savedMirror, savedAxis);
        }

        return parent.AutoSize;
    }

    private static void Run(Rectangle area, TwContainerProps props, List<Entry> entries)
    {
        if (props.Display == TwDisplay.Grid) LayoutGrid(area, props, entries);
        else LayoutFlex(area, props, entries);
    }

    private static int ContentBottom(List<Entry> entries)
    {
        int bottom = 0;
        foreach (var entry in entries)
            bottom = Math.Max(bottom, entry.Control.Bottom + entry.Props.Margin.Bottom);
        return bottom;
    }

    private static List<Entry> Collect(Control parent)
    {
        var entries = new List<Entry>(parent.Controls.Count);
        foreach (Control child in parent.Controls)
        {
            // Ebeveyn henüz görünür değilse (form açılmadan) çocukların Visible'ı hep false döner.
            if (!child.Visible && parent.Visible) continue;
            var data = TwLayout.Get(child);
            entries.Add(new Entry(child, data.Item, data.Measure(child)));
        }
        return entries;
    }

    private static void LayoutFlex(Rectangle area, TwContainerProps container, List<Entry> entries)
    {
        bool row = container.Direction == TwFlexDirection.Row;
        float mainSize = row ? area.Width : area.Height;
        float crossSize = row ? area.Height : area.Width;
        float mainGap = row ? container.GapX : container.GapY;
        float crossGap = row ? container.GapY : container.GapX;

        foreach (var entry in entries)
            entry.Prepare(row, mainSize, crossSize);

        // Satırlara bölme (yalnızca flex-wrap)
        var lines = new List<List<Entry>>();
        var current = new List<Entry>();
        float used = 0;
        foreach (var entry in entries)
        {
            float outer = entry.Hypothetical + entry.MarginMain;
            if (container.Wrap && current.Count > 0 && used + mainGap + outer > mainSize)
            {
                lines.Add(current);
                current = new List<Entry>();
                used = 0;
            }
            used += (current.Count > 0 ? mainGap : 0) + outer;
            current.Add(entry);
        }
        if (current.Count > 0) lines.Add(current);

        float crossCursor = row ? area.Top : area.Left;
        foreach (var line in lines)
        {
            float gaps = mainGap * (line.Count - 1);
            ResolveFlexibleLengths(line, mainSize, gaps);

            // Tek satırlı konteynerde satır, konteynerin tamamını kaplar; çok satırlıda içeriğe göre.
            float lineCross = 0;
            if (!container.Wrap)
            {
                lineCross = crossSize;
            }
            else
            {
                foreach (var entry in line)
                    lineCross = Math.Max(lineCross, entry.CrossBase + entry.MarginCross);
            }

            float occupied = gaps;
            foreach (var entry in line) occupied += entry.Main + entry.MarginMain;
            float free = Math.Max(0, mainSize - occupied);

            float offset = 0, spacing = 0;
            switch (container.Justify)
            {
                case TwJustify.End: offset = free; break;
                case TwJustify.Center: offset = free / 2; break;
                case TwJustify.Between: spacing = line.Count > 1 ? free / (line.Count - 1) : 0; break;
                case TwJustify.Around: spacing = free / line.Count; offset = spacing / 2; break;
                case TwJustify.Evenly: spacing = free / (line.Count + 1); offset = spacing; break;
            }

            float cursor = (row ? area.Left : area.Top) + offset;
            foreach (var entry in line)
            {
                cursor += entry.MarginMainStart;

                var align = entry.Props.Self ?? container.Items;
                float innerCross = lineCross - entry.MarginCross;
                // CSS'ten bilinçli fark: öğe satırın çapraz boyutunu aşmaz (WinForms'ta taşan kısım kesilip kaybolur;
                // daraltılan Label `truncate` ile "…" gösterir).
                float crossLength = align == TwAlign.Stretch && !entry.CrossExplicit
                    ? Clamp(innerCross, entry.CrossMin, entry.CrossMax)
                    : Math.Min(entry.CrossBase, Math.Max(0, innerCross));
                float crossOffset = align switch
                {
                    TwAlign.Center => (innerCross - crossLength) / 2,
                    TwAlign.End => innerCross - crossLength,
                    _ => 0,
                };

                SyncMargin(entry);
                Place(entry.Control, row, cursor, crossCursor + entry.MarginCrossStart + crossOffset, entry.Main, crossLength);
                cursor += entry.Main + entry.MarginMainEnd + mainGap + spacing;
            }

            crossCursor += lineCross + crossGap;
        }
    }

    // CSS Flexbox §9.7'nin sadeleştirilmiş hali: esnek uzunluklar dağıtılır, min/max ihlalleri dondurulup tekrar dağıtılır.
    private static void ResolveFlexibleLengths(List<Entry> line, float available, float gaps)
    {
        float margins = 0, hypothetical = 0;
        foreach (var entry in line)
        {
            margins += entry.MarginMain;
            hypothetical += entry.Hypothetical;
        }

        bool growing = hypothetical + margins + gaps < available;
        foreach (var entry in line)
        {
            float factor = growing ? entry.Props.Grow : entry.Props.Shrink;
            entry.Frozen = factor <= 0
                           || (growing && entry.Base > entry.Hypothetical)
                           || (!growing && entry.Base < entry.Hypothetical);
            entry.Main = entry.Frozen ? entry.Hypothetical : entry.Base;
        }

        for (int pass = 0; pass <= line.Count; pass++)
        {
            float used = margins + gaps, sumGrow = 0, sumScaledShrink = 0;
            int open = 0;
            foreach (var entry in line)
            {
                used += entry.Frozen ? entry.Main : entry.Base;
                if (entry.Frozen) continue;
                open++;
                sumGrow += entry.Props.Grow;
                sumScaledShrink += entry.Props.Shrink * entry.Base;
            }
            if (open == 0) break;

            float free = available - used;
            float totalViolation = 0;
            foreach (var entry in line)
            {
                if (entry.Frozen) continue;
                float target = growing
                    ? entry.Base + free * entry.Props.Grow / sumGrow
                    : sumScaledShrink > 0
                        ? entry.Base + free * (entry.Props.Shrink * entry.Base / sumScaledShrink)
                        : entry.Base;
                float clamped = Clamp(target, entry.MainMin, entry.MainMax);
                entry.Violation = clamped - target;
                totalViolation += entry.Violation;
                entry.Main = clamped;
            }

            if (Math.Abs(totalViolation) < 0.01f)
            {
                foreach (var entry in line) entry.Frozen = true;
                break;
            }

            foreach (var entry in line)
            {
                if (entry.Frozen) continue;
                if ((totalViolation > 0 && entry.Violation > 0) || (totalViolation < 0 && entry.Violation < 0))
                    entry.Frozen = true;
            }
        }
    }

    // CSS grid'in otomatik yerleştirmesi (seyrek): öğeler sırayla, imleçten sonraki ilk boş alana konur; col-span ve
    // row-span'in kapladığı hücreler dolu sayılır. grid-rows-N ile açık satırlar kabın yüksekliğini eşit paylaşır
    // (repeat(N, minmax(0, 1fr))); sonraki (örtük) satırların ve grid-rows'suz satırların yüksekliği içerikten gelir.
    private static void LayoutGrid(Rectangle area, TwContainerProps container, List<Entry> entries)
    {
        int columns = container.Columns;
        float columnWidth = Math.Max(0f, (area.Width - container.GapX * (columns - 1)) / (float)columns);

        var cells = new List<Cell>(entries.Count);
        var occupied = new List<bool[]>();
        int cursorRow = 0, cursorColumn = 0;
        foreach (var entry in entries)
        {
            int span = Math.Min(columns, Math.Max(1, entry.Props.ColumnSpan));
            // row-span-full açık satırların tümünü kaplar; açık satır yoksa tek satırdır.
            int rowSpan = entry.Props.RowSpan == int.MaxValue ? Math.Max(1, container.Rows) : Math.Max(1, entry.Props.RowSpan);
            int row = cursorRow, column = cursorColumn;
            while (true)
            {
                if (column + span > columns)
                {
                    row++;
                    column = 0;
                    continue;
                }
                if (IsFree(occupied, row, column, span, rowSpan)) break;
                column++;
            }
            for (int r = row; r < row + rowSpan; r++)
            {
                while (occupied.Count <= r) occupied.Add(new bool[columns]);
                for (int c = column; c < column + span; c++) occupied[r][c] = true;
            }
            cells.Add(new Cell(entry, row, column, span, rowSpan));
            cursorRow = row;
            cursorColumn = column + span;
        }

        int rowCount = Math.Max(container.Rows, occupied.Count);
        var heights = new float[rowCount];
        var explicitRow = new bool[rowCount];
        if (container.Rows > 0)
        {
            float share = Math.Max(0f, (area.Height - container.GapY * (container.Rows - 1)) / container.Rows);
            for (int r = 0; r < container.Rows; r++)
            {
                heights[r] = share;
                explicitRow[r] = true;
            }
        }
        // Önce tek satırlık öğeler, sonra birden çok satır kaplayanların eksiği kapladıkları içerik satırlarına eşit dağıtılır.
        foreach (var cell in cells)
            if (cell.RowSpan == 1 && !explicitRow[cell.Row])
                heights[cell.Row] = Math.Max(heights[cell.Row], GridHeight(cell.Entry) + cell.Entry.Props.Margin.Vertical);
        foreach (var cell in cells)
        {
            if (cell.RowSpan == 1) continue;
            float need = GridHeight(cell.Entry) + cell.Entry.Props.Margin.Vertical - SpanHeight(heights, container.GapY, cell.Row, cell.RowSpan);
            int autoRows = 0;
            for (int r = cell.Row; r < cell.Row + cell.RowSpan; r++)
                if (!explicitRow[r]) autoRows++;
            if (need <= 0 || autoRows == 0) continue;
            for (int r = cell.Row; r < cell.Row + cell.RowSpan; r++)
                if (!explicitRow[r]) heights[r] += need / autoRows;
        }

        var tops = new float[rowCount];
        float y = area.Top;
        for (int r = 0; r < rowCount; r++)
        {
            tops[r] = y;
            y += heights[r] + container.GapY;
        }

        foreach (var cell in cells)
        {
            var entry = cell.Entry;
            var props = entry.Props;
            var margin = props.Margin;

            float cellX = area.Left + cell.Column * (columnWidth + container.GapX);
            float cellWidth = cell.Span * columnWidth + (cell.Span - 1) * container.GapX;
            float width = props.Width.IsAuto ? cellWidth - margin.Horizontal : props.Width.Resolve(cellWidth);
            width = Clamp(width, props.MinWidth ?? 0, props.MaxWidth ?? Infinity);

            var align = props.Self ?? container.Items;
            float innerHeight = SpanHeight(heights, container.GapY, cell.Row, cell.RowSpan) - margin.Vertical;
            float height = align == TwAlign.Stretch && props.Height.IsAuto
                ? Clamp(innerHeight, props.MinHeight ?? 0, props.MaxHeight ?? Infinity)
                : GridHeight(entry);
            float yOffset = align switch
            {
                TwAlign.Center => (innerHeight - height) / 2,
                TwAlign.End => innerHeight - height,
                _ => 0,
            };

            SyncMargin(entry);
            Place(entry.Control, row: true, cellX + margin.Left, tops[cell.Row] + margin.Top + yOffset, width, height);
        }
    }

    private static bool IsFree(List<bool[]> occupied, int row, int column, int span, int rowSpan)
    {
        for (int r = row; r < row + rowSpan && r < occupied.Count; r++)
            for (int c = column; c < column + span; c++)
                if (occupied[r][c]) return false;
        return true;
    }

    private static float SpanHeight(float[] heights, int gap, int row, int rowSpan)
    {
        float total = (rowSpan - 1) * gap;
        for (int r = row; r < row + rowSpan; r++) total += heights[r];
        return total;
    }

    // Grid satır yüksekliği içerikten belirlenir; oransal yükseklik (h-1/2) burada doğal yüksekliğe düşer.
    private static float GridHeight(Entry entry)
    {
        var props = entry.Props;
        float height = props.Height.IsPixels ? props.Height.Value : entry.Natural.Height;
        return Clamp(height, props.MinHeight ?? 0, props.MaxHeight ?? Infinity);
    }

    private static void Place(Control control, bool row, float main, float cross, float mainLength, float crossLength)
    {
        int main0 = Round(main), main1 = Round(main + mainLength);
        int cross0 = Round(cross), cross1 = Round(cross + crossLength);
        var bounds = row
            ? new Rectangle(main0, cross0, Math.Max(0, main1 - main0), Math.Max(0, cross1 - cross0))
            : new Rectangle(cross0, main0, Math.Max(0, cross1 - cross0), Math.Max(0, main1 - main0));
        if (_mirror) bounds.X = _mirrorAxis - bounds.Right;
        if (control.Bounds != bounds)
            control.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    // Motor Control.Margin'i değil class margin'ini kullanır; ancak WinForms AutoScroll alanını Control.Margin'le
    // hesaplar (Panel varsayılanı 3 px gereksiz yatay kaydırma çubuğu çıkarır). İkisi eşitlenir.
    private static void SyncMargin(Entry entry)
    {
        if (entry.Control.Margin != entry.Props.Margin) entry.Control.Margin = entry.Props.Margin;
    }

    private static int Round(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    // CSS'teki gibi min, max'tan önceliklidir.
    private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));

    private readonly struct Cell
    {
        public Cell(Entry entry, int row, int column, int span, int rowSpan)
        {
            Entry = entry;
            Row = row;
            Column = column;
            Span = span;
            RowSpan = rowSpan;
        }

        public Entry Entry { get; }
        public int Row { get; }
        public int Column { get; }
        public int Span { get; }
        public int RowSpan { get; }
    }

    private sealed class Entry
    {
        public Entry(Control control, TwItemProps props, Size natural)
        {
            Control = control;
            Props = props;
            Natural = natural;
        }

        public Control Control { get; }
        public TwItemProps Props { get; }
        public Size Natural { get; }

        public float Base;
        public float Hypothetical;
        public float Main;
        public float MainMin;
        public float MainMax;
        public float CrossBase;
        public float CrossMin;
        public float CrossMax;
        public bool CrossExplicit;
        public bool Frozen;
        public float Violation;
        public float MarginMainStart;
        public float MarginMainEnd;
        public float MarginCrossStart;
        public float MarginCrossEnd;

        public float MarginMain => MarginMainStart + MarginMainEnd;
        public float MarginCross => MarginCrossStart + MarginCrossEnd;

        public void Prepare(bool row, float mainSize, float crossSize)
        {
            var margin = Props.Margin;
            MarginMainStart = row ? margin.Left : margin.Top;
            MarginMainEnd = row ? margin.Right : margin.Bottom;
            MarginCrossStart = row ? margin.Top : margin.Left;
            MarginCrossEnd = row ? margin.Bottom : margin.Right;

            var mainLength = row ? Props.Width : Props.Height;
            var crossLength = row ? Props.Height : Props.Width;
            float naturalMain = row ? Natural.Width : Natural.Height;
            float naturalCross = row ? Natural.Height : Natural.Width;

            // flex-basis: önce basis, sonra w/h, en son doğal boyut.
            bool autoSize = Props.Basis.IsAuto && mainLength.IsAuto;
            Base = !Props.Basis.IsAuto ? Props.Basis.Resolve(mainSize)
                : !mainLength.IsAuto ? mainLength.Resolve(mainSize)
                : naturalMain;

            // CSS'teki min-width:auto'nun yaklaşığı: boyutu belirtilmemiş öğe doğal boyutunun altına küçülmez.
            MainMin = (row ? Props.MinWidth : Props.MinHeight) ?? (autoSize ? naturalMain : 0);
            MainMax = (row ? Props.MaxWidth : Props.MaxHeight) ?? Infinity;
            Hypothetical = Clamp(Base, MainMin, MainMax);

            CrossMin = (row ? Props.MinHeight : Props.MinWidth) ?? 0;
            CrossMax = (row ? Props.MaxHeight : Props.MaxWidth) ?? Infinity;
            CrossExplicit = !crossLength.IsAuto;
            CrossBase = Clamp(CrossExplicit ? crossLength.Resolve(crossSize) : naturalCross, CrossMin, CrossMax);
        }
    }
}
