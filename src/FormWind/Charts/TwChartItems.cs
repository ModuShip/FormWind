using System;
using System.Collections.ObjectModel;
using System.Drawing;

namespace FormWind;

/// <summary>The value axis a <see cref="TwChartSeries"/> or <see cref="TwChartReferenceLine"/> is measured on.</summary>
public enum TwChartAxis
{
    /// <summary>The left axis.</summary>
    Primary,
    /// <summary>The right axis, with its own scale (e.g. a cumulative percentage over counts).</summary>
    Secondary,
}

/// <summary>A labeled, colored item of a chart control (a guide line, a ring, an event, a step); changes repaint the control.</summary>
public abstract class TwChartItem
{
    private string _label = "";
    private Color _color = Color.Empty;

    private protected TwChartItem()
    {
    }

    /// <summary>Short text shown with the item (empty = none).</summary>
    public string Label
    {
        get => _label;
        set
        {
            _label = value ?? "";
            OnChanged();
        }
    }

    /// <summary>Empty = the control's default for the item (the palette, or the secondary text color for guides).</summary>
    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            OnChanged();
        }
    }

    internal event EventHandler? Changed;

    private protected void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>A dashed horizontal line at a value: a target, a threshold, a limit.</summary>
public sealed class TwChartReferenceLine : TwChartItem
{
    private double _value;
    private TwChartAxis _axis;

    public TwChartReferenceLine()
    {
    }

    public TwChartReferenceLine(double value, string label)
    {
        _value = value;
        Label = label;
    }

    public double Value
    {
        get => _value;
        set
        {
            _value = value;
            OnChanged();
        }
    }

    public TwChartAxis Axis
    {
        get => _axis;
        set
        {
            _axis = value;
            OnChanged();
        }
    }
}

/// <summary>A dashed vertical line at a category with its label on top ("Today", a release, an incident).</summary>
public sealed class TwChartMarker : TwChartItem
{
    private int _index;
    private bool _showLine = true;

    public TwChartMarker()
    {
    }

    public TwChartMarker(int index, string label)
    {
        _index = index;
        Label = label;
    }

    /// <summary>The dashed line under the label; <c>false</c> = only the label (an annotation over a peak).</summary>
    public bool ShowLine
    {
        get => _showLine;
        set
        {
            _showLine = value;
            OnChanged();
        }
    }

    /// <summary>The category the line stands on.</summary>
    public int Index
    {
        get => _index;
        set
        {
            _index = value;
            OnChanged();
        }
    }
}

/// <summary>The items of a chart control (reference lines, markers, rings, events, steps); changes repaint the control.</summary>
public sealed class TwChartItemCollection<T> : Collection<T> where T : TwChartItem
{
    private readonly Action _changed;

    internal TwChartItemCollection(Action changed) => _changed = changed;

    protected override void InsertItem(int index, T item)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));
        base.InsertItem(index, item);
        item.Changed += OnChanged;
        _changed();
    }

    protected override void SetItem(int index, T item)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));
        this[index].Changed -= OnChanged;
        base.SetItem(index, item);
        item.Changed += OnChanged;
        _changed();
    }

    protected override void RemoveItem(int index)
    {
        this[index].Changed -= OnChanged;
        base.RemoveItem(index);
        _changed();
    }

    protected override void ClearItems()
    {
        foreach (var item in this) item.Changed -= OnChanged;
        base.ClearItems();
        _changed();
    }

    private void OnChanged(object? sender, EventArgs e) => _changed();
}
