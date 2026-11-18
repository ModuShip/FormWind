using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Classes of single menu items (<see cref="TailwindProvider.SetClasses(ToolStripItem, string?)"/>). The text color is
/// resolved when the item is drawn, in the state of its menu (dark, hover), so it follows theme changes by itself; the
/// font (<c>text-xs</c>, <c>font-semibold</c>) is set on the item when the classes are given.
/// </summary>
internal static class TwItemClasses
{
    private sealed class Entry
    {
        public Entry(TailwindProvider provider) => Provider = provider;

        public TailwindProvider Provider { get; }
        public string Classes { get; set; } = "";

        // Font sınıflardan geldiyse sınıflar değişince/kalkınca öğe sahibinin fontuna döner; uygulamanın verdiği font korunur.
        public bool SetFont { get; set; }

        // The menu whose font the item's font classes start from; followed so a later menu font (font-sans) reaches the item.
        public ToolStrip? Owner { get; set; }
    }

    private static readonly ConditionalWeakTable<ToolStripItem, Entry> Entries = new ConditionalWeakTable<ToolStripItem, Entry>();

    public static string Get(ToolStripItem item) => Entries.TryGetValue(item, out var entry) ? entry.Classes : "";

    public static void Set(TailwindProvider provider, ToolStripItem item, string classes)
    {
        if (!Entries.TryGetValue(item, out var entry))
        {
            if (classes.Length == 0) return;
            entry = new Entry(provider);
            Entries.Add(item, entry);
            item.OwnerChanged += OnOwnerChanged;
            Follow(item, entry);
        }
        entry.Classes = classes;
        ApplyFont(item, entry);
        if (classes.Length == 0)
        {
            item.OwnerChanged -= OnOwnerChanged;
            Unfollow(entry);
            Entries.Remove(item);
        }
        item.Invalidate();
    }

    private static void OnOwnerChanged(object? sender, System.EventArgs e)
    {
        if (sender is not ToolStripItem item || !Entries.TryGetValue(item, out var entry)) return;
        Follow(item, entry);
        ApplyFont(item, entry);
    }

    private static void Follow(ToolStripItem item, Entry entry)
    {
        Unfollow(entry);
        entry.Owner = item.Owner;
        if (entry.Owner is { } owner) owner.FontChanged += OnOwnerFontChanged;
    }

    private static void Unfollow(Entry entry)
    {
        if (entry.Owner is { } owner) owner.FontChanged -= OnOwnerFontChanged;
        entry.Owner = null;
    }

    private static void OnOwnerFontChanged(object? sender, System.EventArgs e)
    {
        if (sender is not ToolStrip owner) return;
        foreach (ToolStripItem item in owner.Items)
            if (Entries.TryGetValue(item, out var entry)) ApplyFont(item, entry);
    }

    /// <summary>The item's own style in <paramref name="state"/>; <c>null</c> when it has no classes.</summary>
    public static TwStyle? Resolve(ToolStripItem? item, TwState state)
    {
        if (item is null || !Entries.TryGetValue(item, out var entry)) return null;
        return entry.Provider.Theme.Parse(entry.Classes).Resolve(state);
    }

    private static void ApplyFont(ToolStripItem item, Entry entry)
    {
        var theme = entry.Provider.Theme;
        var style = entry.Classes.Length == 0 ? null : theme.Parse(entry.Classes).Resolve(TwState.None);
        if (style is { HasFont: true })
        {
            if (entry.SetFont) item.ResetFont();
            item.Font = TwFonts.Create(style, item.Font, theme);
            entry.SetFont = true;
        }
        else if (entry.SetFont)
        {
            item.ResetFont();
            entry.SetFont = false;
        }
    }
}
