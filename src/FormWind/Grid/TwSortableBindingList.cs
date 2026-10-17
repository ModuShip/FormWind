using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace FormWind;

/// <summary>A list that can be filtered by column values (used by <see cref="TwGrid.EnableFilterMenu"/>).</summary>
public interface ITwFilterableList
{
    /// <summary>Distinct values of a property in all (unfiltered) rows, as display texts, sorted.</summary>
    IReadOnlyList<string> DistinctValues(string property);

    /// <summary>Values shown in the column; <c>null</c> when not filtered.</summary>
    IReadOnlyCollection<string>? GetColumnFilter(string property);

    /// <summary>Shows only the given values in the column (<c>null</c> removes the filter).</summary>
    void SetColumnFilter(string property, IEnumerable<string>? allowed);

    bool HasFilters { get; }

    void ClearFilters();
}

/// <summary>
/// A list the grid can also search, filter by range and group (<see cref="TwGrid.SetSearchText"/>, the range boxes of the
/// filter menu, <see cref="TwGrid.SetGroupBy"/>). Implemented by <see cref="TwSortableBindingList{T}"/>.
/// </summary>
public interface ITwGridSource : ITwFilterableList
{
    /// <summary>Type of a property (<c>null</c> when there is no such property).</summary>
    Type? GetPropertyType(string property);

    /// <summary>The range filter of a property; <c>false</c> when there is none.</summary>
    bool TryGetRangeFilter(string property, out object? min, out object? max);

    /// <summary>
    /// Shows only rows whose value is between <paramref name="min"/> and <paramref name="max"/> (inclusive; <c>null</c> = open
    /// end). Strings are parsed with the current culture. Both <c>null</c> removes the filter.
    /// </summary>
    void SetRangeFilter(string property, object? min, object? max);

    /// <summary>The search text (<c>null</c> = no search).</summary>
    string? SearchText { get; }

    /// <summary>
    /// Shows only rows where one of <paramref name="properties"/> (all properties when <c>null</c>) contains the text,
    /// ignoring case with the current culture. <c>null</c> or empty text removes the search.
    /// </summary>
    void SetSearch(string? text, IEnumerable<string>? properties = null);

    /// <summary>Property the rows are grouped by: rows are ordered by its value first, the current sorting within groups.</summary>
    string? GroupProperty { get; set; }
}

/// <summary>
/// A <see cref="BindingList{T}"/> that can be sorted and filtered by column values. A DataGridView bound to a <c>List&lt;T&gt;</c>
/// or a plain <c>BindingList&lt;T&gt;</c> does not sort when its header is clicked; with this list, columns with
/// <c>SortMode = Automatic</c> sort, the header arrow shows and the <see cref="TwGrid.EnableFilterMenu"/> filter menu works.
/// Strings are compared with the current culture (including Turkish letter order), other values with <see cref="IComparable"/>.
/// </summary>
public class TwSortableBindingList<T> : BindingList<T>, ITwGridSource
{
    /// <summary>
    /// Filter key of blank (<c>null</c> or empty text) values (<see cref="SetColumnFilter"/>). Language-independent;
    /// shown as <see cref="TwTexts.Blank"/> in the filter menu.
    /// </summary>
    public const string EmptyValue = "(blank)";

    private readonly List<T> _all;       // geçerli sırada tüm satırlar (sıralamalar üst üste uygulanır)
    private readonly List<T> _original;  // eklenme sırası (sıralama kaldırılınca)
    private readonly Dictionary<string, HashSet<string>> _filters = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    private readonly Dictionary<string, (object? Min, object? Max)> _ranges = new Dictionary<string, (object? Min, object? Max)>(StringComparer.Ordinal);
    private readonly PropertyDescriptorCollection _properties = TypeDescriptor.GetProperties(typeof(T));
    private PropertyDescriptor? _sortProperty;
    private ListSortDirection _sortDirection;
    private bool _sorted;
    private bool _rebuilding;
    private string? _search;
    private PropertyDescriptor[] _searchProperties = Array.Empty<PropertyDescriptor>();
    private string? _groupProperty;

    public TwSortableBindingList() : this(Enumerable.Empty<T>())
    {
    }

    public TwSortableBindingList(IEnumerable<T> items) : base(items.ToList())
    {
        _all = new List<T>(Items);
        _original = new List<T>(Items);
    }

    /// <summary>All rows, regardless of filtering.</summary>
    public IReadOnlyList<T> AllItems => _all;

    protected override bool SupportsSortingCore => true;

    protected override bool IsSortedCore => _sorted;

    protected override PropertyDescriptor? SortPropertyCore => _sortProperty;

    protected override ListSortDirection SortDirectionCore => _sortDirection;

    protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
    {
        _sortProperty = prop;
        _sortDirection = direction;
        _sorted = true;
        SortAll(prop, direction);
        Rebuild();
    }

    protected override void RemoveSortCore()
    {
        _sorted = false;
        _sortProperty = null;
        _all.Clear();
        _all.AddRange(_original);   // eklenme sırası
        Rebuild();
    }

    // Kararlı sıralama, önceki sıranın üzerine: eşit değerler önceki sıralamadaki yerini korur (önce ada, sonra yaşa göre
    // sıralanan listede aynı yaştakiler ada göre kalır).
    private void SortAll(PropertyDescriptor prop, ListSortDirection direction)
    {
        int Compare(T a, T b)
        {
            int result = CompareValues(prop.GetValue(a), prop.GetValue(b));
            return direction == ListSortDirection.Ascending ? result : -result;
        }

        var indexed = _all.Select((item, index) => (Item: item, Index: index)).ToList();
        indexed.Sort((p, q) =>
        {
            int c = Compare(p.Item, q.Item);
            return c != 0 ? c : p.Index.CompareTo(q.Index);
        });
        _all.Clear();
        _all.AddRange(indexed.Select(p => p.Item));
    }

    internal static int CompareValues(object? x, object? y) =>
        x is null ? (y is null ? 0 : -1)
        : y is null ? 1
        : x is string sx && y is string sy ? CultureInfo.CurrentCulture.CompareInfo.Compare(sx, sy, CompareOptions.IgnoreCase)
        : x is IComparable comparable && x.GetType() == y.GetType() ? comparable.CompareTo(y)
        : string.Compare(x.ToString(), y.ToString(), StringComparison.CurrentCulture);

    public bool HasFilters => _filters.Count > 0 || _ranges.Count > 0;

    public IReadOnlyList<string> DistinctValues(string property)
    {
        var descriptor = Property(property);
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        var values = _all.Select(item => Display(descriptor.GetValue(item))).Distinct(StringComparer.Ordinal).ToList();
        values.Sort((a, b) => a == EmptyValue ? -1 : b == EmptyValue ? 1 : compare.Compare(a, b, CompareOptions.IgnoreCase));
        return values;
    }

    public IReadOnlyCollection<string>? GetColumnFilter(string property) =>
        _filters.TryGetValue(property, out var allowed) ? allowed : null;

    public void SetColumnFilter(string property, IEnumerable<string>? allowed)
    {
        Property(property);
        if (allowed is null) _filters.Remove(property);
        else _filters[property] = new HashSet<string>(allowed, StringComparer.Ordinal);
        Rebuild();
    }

    /// <summary>Removes the value and range filters (the search stays).</summary>
    public void ClearFilters()
    {
        _filters.Clear();
        _ranges.Clear();
        Rebuild();
    }

    public Type? GetPropertyType(string property) => _properties.Find(property, false)?.PropertyType;

    public bool TryGetRangeFilter(string property, out object? min, out object? max)
    {
        bool found = _ranges.TryGetValue(property, out var range);
        min = range.Min;
        max = range.Max;
        return found;
    }

    public void SetRangeFilter(string property, object? min, object? max)
    {
        var descriptor = Property(property);
        var low = ToPropertyType(descriptor, min);
        var high = ToPropertyType(descriptor, max);
        if (low is null && high is null) _ranges.Remove(property);
        else _ranges[property] = (low, high);
        Rebuild();
    }

    public string? SearchText => _search;

    public void SetSearch(string? text, IEnumerable<string>? properties = null)
    {
        _search = string.IsNullOrEmpty(text) ? null : text;
        _searchProperties = properties is null
            ? _properties.Cast<PropertyDescriptor>().ToArray()
            : properties.Select(p => _properties.Find(p, false)).Where(p => p != null).Select(p => p!).ToArray();
        Rebuild();
    }

    public string? GroupProperty
    {
        get => _groupProperty;
        set
        {
            if (value != null) Property(value);
            if (_groupProperty == value) return;
            _groupProperty = value;
            Rebuild();
        }
    }

    // Aralık sınırları özelliğin tipine çevrilir (metinler geçerli kültürle: "1.250,50", "31.12.2025").
    private static object? ToPropertyType(PropertyDescriptor property, object? value)
    {
        if (value is null || (value is string blank && blank.Trim().Length == 0)) return null;
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (type.IsInstanceOfType(value)) return value;
        if (value is string text) return TypeDescriptor.GetConverter(type).ConvertFromString(null, CultureInfo.CurrentCulture, text.Trim());
        return Convert.ChangeType(value, type, CultureInfo.CurrentCulture);
    }

    // Görünen liste: süzmeden geçen satırlar, geçerli sırada.
    private void Rebuild()
    {
        if (Items is not List<T> items) return;
        var visible = _all.Where(Passes).ToList();
        if (_groupProperty != null)
        {
            // OrderBy kararlıdır: grup içinde geçerli sıralama korunur.
            var group = Property(_groupProperty);
            visible = visible.OrderBy(item => group.GetValue(item), Comparer<object?>.Create(CompareValues)).ToList();
        }

        _rebuilding = true;
        try
        {
            items.Clear();
            items.AddRange(visible);
        }
        finally
        {
            _rebuilding = false;
        }
        OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
    }

    private bool Passes(T item)
    {
        foreach (var filter in _filters)
            if (!filter.Value.Contains(Display(Property(filter.Key).GetValue(item)))) return false;
        foreach (var range in _ranges)
        {
            var value = Property(range.Key).GetValue(item);
            if (value is null) return false;
            if (range.Value.Min != null && CompareValues(value, range.Value.Min) < 0) return false;
            if (range.Value.Max != null && CompareValues(value, range.Value.Max) > 0) return false;
        }
        if (_search != null && !_searchProperties.Any(p => Contains(p.GetValue(item), _search))) return false;
        return true;
    }

    /// <summary>Whether the value's display text contains the search text (current culture, ignoring case).</summary>
    internal static bool Contains(object? value, string search)
    {
        if (value is null) return false;
        var text = value is IFormattable formattable ? formattable.ToString(null, CultureInfo.CurrentCulture) : value.ToString();
        return text != null && CultureInfo.CurrentCulture.CompareInfo.IndexOf(text, search, CompareOptions.IgnoreCase) >= 0;
    }

    private PropertyDescriptor Property(string name) =>
        _properties.Find(name, false) ?? throw new ArgumentException($"Type {typeof(T).Name} has no property '{name}'.", nameof(name));

    internal static string Display(object? value) => value switch
    {
        null => EmptyValue,
        string text when text.Length == 0 => EmptyValue,
        IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
        _ => value.ToString() ?? EmptyValue,
    };

    // Görünen listeye yapılan eklemeler/silmeler tüm satırlara da yansır.
    private bool _addingNew;

    // Grid'in yeni satırı (AddNew) sonda kalmalı; gruplanmış listeye kodla eklenen satır kendi grubunun sonuna girer.
    protected override object? AddNewCore()
    {
        _addingNew = true;
        try
        {
            return base.AddNewCore();
        }
        finally
        {
            _addingNew = false;
        }
    }

    protected override void InsertItem(int index, T item)
    {
        if (!_rebuilding && !_addingNew && _groupProperty != null && index == Count)
        {
            var group = Property(_groupProperty);
            var key = group.GetValue(item);
            while (index > 0 && CompareValues(group.GetValue(this[index - 1]), key) > 0) index--;
        }
        base.InsertItem(index, item);
        if (_rebuilding) return;
        _all.Add(item);
        _original.Add(item);
    }

    protected override void RemoveItem(int index)
    {
        if (!_rebuilding)
        {
            _all.Remove(this[index]);
            _original.Remove(this[index]);
        }
        base.RemoveItem(index);
    }

    protected override void SetItem(int index, T item)
    {
        if (!_rebuilding)
        {
            int at = _all.IndexOf(this[index]);
            if (at >= 0) _all[at] = item;
            at = _original.IndexOf(this[index]);
            if (at >= 0) _original[at] = item;
        }
        base.SetItem(index, item);
    }

    protected override void ClearItems()
    {
        if (!_rebuilding)
        {
            _all.Clear();
            _original.Clear();
        }
        base.ClearItems();
    }
}
