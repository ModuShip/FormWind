using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>A row of <see cref="TwCommandPalette"/>: what it shows and what it does.</summary>
public sealed class TwCommand
{
    public TwCommand(string text, Action execute)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public string Text { get; }

    public Action Execute { get; }

    /// <summary>Heading the command is listed under ("Sayfalar", "İşlemler"); <c>null</c> = no heading.</summary>
    public string? Group { get; set; }

    /// <summary>Icon before the text (<c>null</c> = none).</summary>
    public string? IconName { get; set; }

    /// <summary>Shortcut hint drawn on the right ("Ctrl+S"); only shown, not registered.</summary>
    public string? Shortcut { get; set; }

    /// <summary>More words the command is found by (synonyms, the English name).</summary>
    public string? Keywords { get; set; }

    public override string ToString() => Text;
}

/// <summary>
/// Command palette: Ctrl+K (<see cref="Shortcut"/>) opens a search box over the window; typing filters the
/// <see cref="Commands"/> (ignoring case and accents, best matches first) under their group headings; ↑/↓ choose, Enter runs,
/// Esc or a click outside closes. <see cref="AddSearch"/> adds "Search "…" in records" rows for what was typed, and
/// <see cref="QueryCommands"/> can add rows of its own for a query (matching records).
/// <code>
/// var palette = new TwCommandPalette();
/// palette.Commands.Add(new TwCommand("Duruş kayıtları", () => ShowPage(records)) { Group = "Sayfalar", IconName = "list" });
/// palette.AddSearch("kayıtlar", text => SearchRecords(text));
/// palette.Attach(this);
/// </code>
/// </summary>
[ToolboxItem(false)]
public class TwCommandPalette : Component, IMessageFilter
{
    private readonly List<(string Source, Action<string> Search, string? Group)> _searches = new List<(string, Action<string>, string?)>();
    private readonly List<Form> _forms = new List<Form>();
    private bool _filtering;
    private TwPaletteWindow? _window;

    public TwCommandPalette()
    {
    }

    public TwCommandPalette(IContainer container) : this() => container.Add(this);

    /// <summary>Raised after a command ran.</summary>
    public event EventHandler<TwCommandEventArgs>? Executed;

    /// <summary>The commands, in the order they are listed when nothing is typed.</summary>
    public List<TwCommand> Commands { get; } = new List<TwCommand>();

    /// <summary>Extra rows for a query (records that match it); called as the user types.</summary>
    public Func<string, IEnumerable<TwCommand>>? QueryCommands { get; set; }

    /// <summary>The key that opens the palette in the <see cref="Attach">attached</see> forms.</summary>
    public Keys Shortcut { get; set; } = Keys.Control | Keys.K;

    /// <summary>Hint of the search box; <see cref="TwTexts.CommandPlaceholder"/> when not set.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Most rows listed (headings not counted).</summary>
    public int MaxResults { get; set; } = 50;

    /// <summary>Whether the palette is open.</summary>
    public bool IsOpen => _window is { IsDisposed: false, Visible: true };

    /// <summary>The open window (for tests).</summary>
    internal TwPaletteWindow? Window => IsOpen ? _window : null;

    /// <summary>
    /// Adds a "Search "<i>query</i>" in <paramref name="source"/>" row whenever something is typed; choosing it calls
    /// <paramref name="search"/> with the query.
    /// </summary>
    public void AddSearch(string source, Action<string> search, string? group)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (search is null) throw new ArgumentNullException(nameof(search));
        _searches.Add((source, search, group));
    }

    /// <summary>Ctrl+K (<see cref="Shortcut"/>) opens the palette over <paramref name="form"/> from any of its controls.</summary>
    public void Attach(Form form)
    {
        if (form is null) throw new ArgumentNullException(nameof(form));
        if (_forms.Contains(form)) return;
        _forms.Add(form);
        form.Disposed += (_, _) => Detach(form);
        if (!_filtering)
        {
            Application.AddMessageFilter(this);
            _filtering = true;
        }
    }

    public void Detach(Form form)
    {
        _forms.Remove(form);
        if (_forms.Count == 0 && _filtering)
        {
            Application.RemoveMessageFilter(this);
            _filtering = false;
        }
    }

    bool IMessageFilter.PreFilterMessage(ref Message m)
    {
        const int WmKeyDown = 0x0100, WmSysKeyDown = 0x0104;
        if (m.Msg != WmKeyDown && m.Msg != WmSysKeyDown) return false;
        var keys = (Keys)(m.WParam.ToInt64() & 0xffff) | Control.ModifierKeys;
        if (keys != Shortcut) return false;
        var form = Control.FromChildHandle(m.HWnd)?.FindForm();
        if (form is null || !_forms.Contains(form)) return false;
        Show(form);
        return true;
    }

    /// <summary>Opens the palette over <paramref name="owner"/>.</summary>
    public void Show(Form owner)
    {
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        if (IsOpen)
        {
            _window!.Activate();
            return;
        }
        _window?.Dispose();
        _window = new TwPaletteWindow(this, owner);
        _window.Show(owner);
    }

    public void Close() => _window?.Close();

    /// <summary>The rows for a query: matching commands (best first), then the query rows and the search rows.</summary>
    public IReadOnlyList<TwCommand> Find(string? query)
    {
        query = query?.Trim() ?? "";
        var result = new List<TwCommand>();
        if (query.Length == 0) result.AddRange(Commands.Take(Math.Max(0, MaxResults)));
        else
        {
            var folded = TwSelect.Fold(query);
            var scored = new List<(int Score, int Index, TwCommand Command)>();
            for (int i = 0; i < Commands.Count; i++)
            {
                int score = Score(Commands[i], folded);
                if (score >= 0) scored.Add((score, i, Commands[i]));
            }
            result.AddRange(scored.OrderBy(x => x.Score).ThenBy(x => x.Index).Take(Math.Max(0, MaxResults)).Select(x => x.Command));
            if (QueryCommands?.Invoke(query) is IEnumerable<TwCommand> extra) result.AddRange(extra.Where(c => c != null));
            foreach (var (source, search, group) in _searches)
            {
                var text = string.Format(CultureInfo.CurrentCulture, TwTexts.Current.SearchIn, query, source);
                var typed = query;
                result.Add(new TwCommand(text, () => search(typed)) { Group = group, IconName = "search" });
            }
        }
        return result;
    }

    // 0: metnin başı, 1: bir kelimenin başı, 2: metnin içinde, 3: anahtar kelimelerde ya da grubunda; -1: eşleşmez.
    private static int Score(TwCommand command, string folded)
    {
        var compare = CultureInfo.InvariantCulture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        var text = TwSelect.Fold(command.Text);
        if (compare.IsPrefix(text, folded, options)) return 0;
        int index = compare.IndexOf(text, folded, options);
        if (index > 0 && !char.IsLetterOrDigit(text[index - 1])) return 1;
        if (index >= 0) return 2;
        foreach (var extra in new[] { command.Keywords, command.Group })
            if (!string.IsNullOrEmpty(extra) && compare.IndexOf(TwSelect.Fold(extra!), folded, options) >= 0) return 3;
        return -1;
    }

    internal void Run(TwCommand command, Form owner)
    {
        Close();
        // Komut palet kapandıktan sonra çalışır: açtığı form ya da diyalog sahibin üstüne gelsin.
        void Execute()
        {
            command.Execute();
            Executed?.Invoke(this, new TwCommandEventArgs(command));
        }
        if (owner.IsHandleCreated && !owner.IsDisposed) owner.BeginInvoke(new Action(Execute));
        else Execute();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_filtering) Application.RemoveMessageFilter(this);
            _filtering = false;
            _forms.Clear();
            _window?.Dispose();
            _window = null;
        }
        base.Dispose(disposing);
    }
}

public sealed class TwCommandEventArgs : EventArgs
{
    public TwCommandEventArgs(TwCommand command) => Command = command;

    public TwCommand Command { get; }
}

/// <summary>The palette window: a search box and the result list, over the top of the owner.</summary>
internal sealed class TwPaletteWindow : Form
{
    private readonly TwCommandPalette _palette;
    private readonly Form _owner;
    private readonly TextBox _search;
    private readonly TwPaletteList _list;
    private readonly bool _dark;

    public TwPaletteWindow(TwCommandPalette palette, Form owner)
    {
        _palette = palette;
        _owner = owner;
        _dark = TwColorsOf.EffectiveBack(owner).GetBrightness() < 0.4f;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        Font = owner.Font;
        BackColor = _dark ? ColorTranslator.FromHtml("#1f2937") : Color.White;
        ForeColor = _dark ? ColorTranslator.FromHtml("#f3f4f6") : ColorTranslator.FromHtml("#111827");

        _search = new TextBox { BorderStyle = BorderStyle.None, BackColor = BackColor, ForeColor = ForeColor, Font = new Font(owner.Font.FontFamily, owner.Font.Size * 1.15f) };
        TwSelect.SetCueBanner(_search, palette.Placeholder ?? TwTexts.Current.CommandPlaceholder);
        _list = new TwPaletteList(_dark) { Font = owner.Font, BackColor = BackColor, ForeColor = ForeColor };
        _list.RowClicked += (_, _) => RunSelected();
        Controls.Add(_list);
        Controls.Add(_search);
        _search.TextChanged += (_, _) => Refill();
        _search.KeyDown += OnSearchKeyDown;
        Refill();
    }

    internal TextBox SearchBox => _search;

    internal TwPaletteList List => _list;

    private int Px(float value) => (int)Math.Round(value * TwDpi.DeviceDpi(_owner) / 96f);

    protected override bool ShowWithoutActivation => false;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        TwNativeCorners.Round(this);
        Place();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _search.Focus();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        // Dışarı tıklanınca kapanır (komut çalışırken de; o zaman zaten kapanıyor).
        if (!IsDisposed) BeginInvoke(new Action(Close));
    }

    private void Refill()
    {
        _list.SetRows(_palette.Find(_search.Text));
        if (IsHandleCreated) Place();
    }

    private void Place()
    {
        int width = Math.Min(Px(560), Math.Max(Px(240), _owner.ClientSize.Width - Px(32)));
        int pad = Px(12);
        int searchHeight = _search.PreferredHeight;
        _search.SetBounds(pad, pad, width - pad * 2, searchHeight);
        int listTop = pad + searchHeight + pad;
        int listHeight = Math.Min(_list.ContentHeight, Px(10 * 36));
        _list.SetBounds(Px(6), listTop, width - Px(12), Math.Max(_list.MinimumHeight, listHeight));
        var client = _owner.RectangleToScreen(_owner.ClientRectangle);
        var size = new Size(width, _list.Bottom + Px(6));
        Bounds = new Rectangle(client.Left + (client.Width - width) / 2, client.Top + Px(56), size.Width, size.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var border = _dark ? ColorTranslator.FromHtml("#374151") : ColorTranslator.FromHtml("#e5e7eb");
        using var pen = new Pen(border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        int y = _search.Bottom + Px(12) / 2;
        e.Graphics.DrawLine(pen, 0, y, Width, y);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Down: _list.MoveSelection(1); break;
            case Keys.Up: _list.MoveSelection(-1); break;
            case Keys.PageDown: _list.MoveSelection(8); break;
            case Keys.PageUp: _list.MoveSelection(-8); break;
            case Keys.Enter: RunSelected(); break;
            case Keys.Escape: Close(); break;
            default: return;
        }
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    internal void RunSelected()
    {
        if (_list.Selected is TwCommand command) _palette.Run(command, _owner);
    }
}

/// <summary>The result rows of the palette: group headings and commands; one command is selected.</summary>
internal sealed class TwPaletteList : Control
{
    private readonly List<(string? Header, TwCommand? Command)> _rows = new List<(string?, TwCommand?)>();
    private readonly bool _dark;
    private int _selected = -1;
    private int _scroll;

    public TwPaletteList(bool dark)
    {
        _dark = dark;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
    }

    public event EventHandler? RowClicked;

    private int Px(float value) => (int)Math.Round(value * TwDpi.DeviceDpi(this) / 96f);

    private int ItemHeight => Math.Max(Px(34), Font.Height + Px(14));

    private int HeaderHeight => Math.Max(Px(26), Font.Height + Px(8));

    private int RowHeight(int index) => _rows[index].Command is null ? HeaderHeight : ItemHeight;

    public int ContentHeight
    {
        get
        {
            int height = 0;
            for (int i = 0; i < _rows.Count; i++) height += RowHeight(i);
            return Math.Max(height, MinimumHeight);
        }
    }

    public int MinimumHeight => ItemHeight;

    /// <summary>The rows shown: headings and command texts, in order (for tests).</summary>
    internal IEnumerable<string> RowTexts => _rows.Select(r => r.Command?.Text ?? "# " + r.Header);

    public TwCommand? Selected => _selected >= 0 && _selected < _rows.Count ? _rows[_selected].Command : null;

    public void SetRows(IReadOnlyList<TwCommand> commands)
    {
        _rows.Clear();
        // Gruplar ilk göründükleri sırayla; her grubun komutları kendi sırasıyla bir başlığın altında.
        var order = new List<string?>();
        foreach (var command in commands)
            if (!order.Contains(command.Group)) order.Add(command.Group);
        foreach (var group in order)
        {
            if (!string.IsNullOrEmpty(group)) _rows.Add((group, null));
            foreach (var command in commands)
                if (command.Group == group) _rows.Add((null, command));
        }
        _selected = _rows.FindIndex(r => r.Command != null);
        _scroll = 0;
        Invalidate();
    }

    public void MoveSelection(int delta)
    {
        if (_rows.Count == 0) return;
        int step = Math.Sign(delta), count = Math.Abs(delta), index = _selected;
        while (count > 0)
        {
            int next = index + step;
            while (next >= 0 && next < _rows.Count && _rows[next].Command is null) next += step;
            if (next < 0 || next >= _rows.Count) break;
            index = next;
            count--;
        }
        if (index == _selected) return;
        _selected = index;
        EnsureVisible();
        Invalidate();
    }

    private int RowTop(int index)
    {
        int y = 0;
        for (int i = 0; i < index; i++) y += RowHeight(i);
        return y;
    }

    private void EnsureVisible()
    {
        if (_selected < 0) return;
        int top = RowTop(_selected), bottom = top + RowHeight(_selected);
        // Başlığıyla birlikte görünsün.
        if (_selected > 0 && _rows[_selected - 1].Command is null) top -= HeaderHeight;
        if (top < _scroll) _scroll = Math.Max(0, top);
        else if (bottom > _scroll + Height) _scroll = bottom - Height;
    }

    private int IndexAt(Point point)
    {
        int y = -_scroll;
        for (int i = 0; i < _rows.Count; i++)
        {
            int h = RowHeight(i);
            if (point.Y >= y && point.Y < y + h) return _rows[i].Command is null ? -1 : i;
            y += h;
        }
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index = IndexAt(e.Location);
        if (index < 0 || index == _selected) return;
        _selected = index;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int index = IndexAt(e.Location);
        if (e.Button != MouseButtons.Left || index < 0) return;
        _selected = index;
        RowClicked?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        _scroll = Math.Max(0, Math.Min(ContentHeight - Height, _scroll - e.Delta / 120 * ItemHeight));
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using (var back = new SolidBrush(BackColor)) g.FillRectangle(back, ClientRectangle);
        var muted = _dark ? ColorTranslator.FromHtml("#9ca3af") : ColorTranslator.FromHtml("#6b7280");
        var selectedBack = _dark ? ColorTranslator.FromHtml("#374151") : ColorTranslator.FromHtml("#f3f4f6");
        const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        bool rtl = TwRtl.NeedsMirroring(this);
        if (_rows.Count == 0)
        {
            TextRenderer.DrawText(g, TwTexts.Current.NoResults, Font, ClientRectangle, muted, flags | TextFormatFlags.HorizontalCenter);
            return;
        }
        int y = -_scroll;
        int em = TwIconRenderer.EmPixels(Font.SizeInPoints, TwDpi.DeviceDpi(this));
        for (int i = 0; i < _rows.Count; i++)
        {
            int h = RowHeight(i);
            var row = new Rectangle(0, y, Width, h);
            y += h;
            if (row.Bottom < 0 || row.Top > Height) continue;
            var (header, command) = _rows[i];
            if (command is null)
            {
                var headerBounds = Rectangle.FromLTRB(Px(10), row.Top, Width - Px(10), row.Bottom);
                TextRenderer.DrawText(g, header, Font, headerBounds, muted, flags | (rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : 0));
                continue;
            }
            if (i == _selected) TwPainter.FillRounded(g, Rectangle.Inflate(row, 0, -Px(1)), Px(6), selectedBack);
            int left = Px(10), right = Width - Px(10);
            if (command.IconName != null)
            {
                var icon = new Rectangle(rtl ? right - Px(20) : left, row.Top, Px(20), row.Height);
                TwIconRenderer.DrawIcon(g, command.IconName, em, icon, muted);
                if (rtl) right -= Px(28);
                else left += Px(28);
            }
            if (!string.IsNullOrEmpty(command.Shortcut))
            {
                int width = TextRenderer.MeasureText(command.Shortcut, Font).Width;
                var hint = rtl ? new Rectangle(left, row.Top, width, row.Height) : new Rectangle(right - width, row.Top, width, row.Height);
                TextRenderer.DrawText(g, command.Shortcut, Font, hint, muted, flags);
                if (rtl) left += width + Px(12);
                else right -= width + Px(12);
            }
            TextRenderer.DrawText(g, command.Text, Font, Rectangle.FromLTRB(left, row.Top, right, row.Bottom), ForeColor,
                flags | (rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : 0));
        }
    }
}
