using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing.Design;
using System.Linq;
using System.Windows.Forms;
using FormWind.Design;

namespace FormWind;

/// <summary>
/// Adds a <c>Classes</c> property to every control on the form (the same mechanism as ToolTip).
/// Classes are parsed, merged for the control's state (hover, focus, active, disabled, dark)
/// and applied with the matching <see cref="IStyleApplier"/>.
/// </summary>
[Designer(typeof(Design.TailwindProviderDesigner))]
[ProvideProperty("Classes", typeof(Control))]
[ProvideProperty("ColumnClasses", typeof(DataGridViewColumn))]
[ToolboxItemFilter("System.Windows.Forms")]
[Description("Kontrollere Tailwind benzeri 'Classes' property'si ekler.")]
public class TailwindProvider : Component, IExtenderProvider, ISupportInitialize
{
    private readonly Dictionary<Control, TwBinding> _bindings = new Dictionary<Control, TwBinding>();
    private readonly Dictionary<DataGridViewColumn, TwColumnBinding> _columns = new Dictionary<DataGridViewColumn, TwColumnBinding>();
    private readonly Dictionary<DataGridView, TwRowRule> _rowRules = new Dictionary<DataGridView, TwRowRule>();
    private readonly Timer _hoverTimer;
    private TwTheme? _theme;
    private bool _darkMode;
    private bool _designTimePreview = true;
    private bool _scaleWithDpi = true;
    private bool _darkScrollbars = true;
    private bool _darkToolTips = true;
    private bool _respectHighContrast = true;
    private bool _initializing;
    private bool _autoStyle;
    private ContainerControl? _containerControl;
    // AutoStyle'ın gezdiği ve ControlAdded'ına abone olunan konteynerler.
    private readonly HashSet<Control> _autoContainers = new HashSet<Control>();
    // Çocuklarının kopyalanmış zeminleri bırakılmış konteynerler (bkz. FollowParentBackColor); her biri bir kez.
    private readonly HashSet<Control> _backColorsChecked = new HashSet<Control>();

    public TailwindProvider()
    {
        _hoverTimer = new Timer { Interval = 100 };
        _hoverTimer.Tick += OnHoverTick;
        TwTheme.CurrentChanged += OnCurrentThemeChanged;
        TwHighContrast.Changed += OnHighContrastChanged;
        TwTextScale.Changed += OnTextScaleChanged;
        TwFocusVisible.EnsureInstalled();
    }

    public TailwindProvider(IContainer container)
        : this()
    {
        if (container is null) throw new ArgumentNullException(nameof(container));
        container.Add(this);
    }

    /// <summary>Raised when a control's classes contain unknown values. Written to the Debug output when nobody listens.</summary>
    public event EventHandler<TwUnknownClassesEventArgs>? UnknownClasses;

    /// <summary>
    /// Raised when an error occurs while applying styles (e.g. a custom <see cref="IStyleApplier"/> or a
    /// <see cref="SetRowClasses"/> rule threw). The error is swallowed: the application keeps running and the control keeps
    /// the style applied so far. Written to the Debug output when nobody listens; a listener may rethrow the exception.
    /// Critical exceptions such as out-of-memory are not caught.
    /// </summary>
    public event EventHandler<TwStyleErrorEventArgs>? StyleError;

    [Category("FormWind"), DefaultValue(false), Description("Enables the dark: variants.")]
    public bool DarkMode
    {
        get => _darkMode;
        set
        {
            if (_darkMode == value) return;
            _darkMode = value;
            UpdateNativeToolTips();
            RefreshAll();
            DarkModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>When <see cref="DarkMode"/> changes (after the controls have been restyled).</summary>
    public event EventHandler? DarkModeChanged;

    /// <summary>
    /// When the theme in use changes: <see cref="Theme"/> is set, or <see cref="TwTheme.Current"/> changes while the provider has
    /// no theme of its own (after the controls have been restyled).
    /// </summary>
    public event EventHandler? ThemeChanged;

    [Category("FormWind"), DefaultValue(true), Description("Stilleri Designer'da da uygular.")]
    public bool DesignTimePreview
    {
        get => _designTimePreview;
        set
        {
            if (_designTimePreview == value) return;
            _designTimePreview = value;
            // Önizleme kapanınca kontroller özgün değerlerine döner; yoksa önizlemenin renkleri kontrolde kalıp Designer.cs'e
            // yazılıyordu.
            if (!value && DesignMode)
            {
                foreach (var binding in _bindings.Values.ToList()) binding.Restore();
            }
            else RefreshAll();
        }
    }

    /// <summary>Designer: the control's values before the classes are put back while it is written to Designer.cs.</summary>
    internal bool RestoreForSerialization(Control control)
    {
        if (!DesignMode || !_designTimePreview || !_bindings.TryGetValue(control, out var binding)) return false;
        binding.Restore();
        return true;
    }

    /// <summary>Designer: applies the preview again after <see cref="RestoreForSerialization"/>.</summary>
    internal void ReapplyAfterSerialization(Control control)
    {
        if (_bindings.TryGetValue(control, out var binding)) binding.Refresh(force: true);
    }

    /// <summary>
    /// Scales spacing, size, corner and border values with the control's DPI (96 DPI = 1).
    /// Turn it off in applications whose form layout does not scale with DPI (AutoScale not working);
    /// otherwise class values grow while control sizes stay fixed.
    /// </summary>
    [Category("FormWind"), DefaultValue(true), Description("Scales class pixel values with the control's DPI.")]
    public bool ScaleWithDpi
    {
        get => _scaleWithDpi;
        set
        {
            if (_scaleWithDpi == value) return;
            _scaleWithDpi = value;
            RefreshAll();
        }
    }

    /// <summary>
    /// In Windows High Contrast mode, uses system colors instead of class colors (background and text return to their original
    /// values, selected and hovered parts get the highlight color, shadows and gradients are not drawn); <c>forced-colors:</c>
    /// variants still apply. Spacing, font size, corners and layout do not change. Styles are reapplied when the mode changes.
    /// </summary>
    [Category("FormWind"), DefaultValue(true), Description("Uses system colors in High Contrast mode.")]
    public bool RespectHighContrast
    {
        get => _respectHighContrast;
        set
        {
            if (_respectHighContrast == value) return;
            _respectHighContrast = value;
            RefreshAll();
            UpdateNativeToolTips();
        }
    }

    /// <summary>Whether system colors are used (High Contrast is on and <see cref="RespectHighContrast"/>).</summary>
    [Browsable(false)]
    public bool HighContrastActive => _respectHighContrast && !DesignMode && TwHighContrast.IsActive;

    /// <summary>Control-independent state: dark mode and High Contrast.</summary>
    internal TwState BaseState =>
        (_darkMode ? TwState.Dark : TwState.None) | (HighContrastActive ? TwState.HighContrast : TwState.None);

    // Windows'un metin boyutu değişti (ya da TwFonts.RespectTextScale): text-* boyutları yeniden hesaplanır.
    private void OnTextScaleChanged(object? sender, EventArgs e) => RefreshAll();

    private void OnHighContrastChanged(object? sender, EventArgs e)
    {
        if (!_respectHighContrast) return;
        RefreshAll();
        UpdateNativeToolTips();
    }

    /// <summary>
    /// Also darkens native scrollbars (panel, TextBox, ListBox, TreeView, ListView, DataGridView) in dark mode. Requires
    /// Windows 10 1809+ and uses an undocumented theme name; turn it off if it causes problems.
    /// </summary>
    [Category("FormWind"), DefaultValue(true), Description("Also darkens native scrollbars in dark mode.")]
    public bool DarkScrollbars
    {
        get => _darkScrollbars;
        set
        {
            if (_darkScrollbars == value) return;
            _darkScrollbars = value;
            RefreshAll();
        }
    }

    /// <summary>
    /// Also darkens native tooltip windows (ToolTip components, ToolStrip item tooltips) in dark mode. Because tooltips
    /// belong to the application's components, the effect is thread-wide. Requires Windows 10 1809+ and uses an undocumented
    /// theme name; turn it off if it causes problems.
    /// </summary>
    [Category("FormWind"), DefaultValue(true), Description("Also darkens native tooltips in dark mode.")]
    public bool DarkToolTips
    {
        get => _darkToolTips;
        set
        {
            if (_darkToolTips == value) return;
            _darkToolTips = value;
            UpdateNativeToolTips();
        }
    }

    private void UpdateNativeToolTips()
    {
        // Yüksek Karşıtlık'ta ipuçları Windows'un karşıtlık renkleriyle kalır.
        if (!DesignMode) TwNativeToolTips.SetDark(this, _darkMode && _darkToolTips && !HighContrastActive);
    }

    /// <summary>The theme used by this provider. <see cref="TwTheme.Current"/> when not set.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwTheme Theme
    {
        get => _theme ?? TwTheme.Current;
        set
        {
            _theme = value;
            ReparseAll();
        }
    }

    internal bool IsDesignMode => DesignMode;

    /// <summary>
    /// Gives the controls of <see cref="ContainerControl"/> the theme's component for their type (<see cref="TwTheme.AutoStyle"/>:
    /// form → <c>page</c>, button → <c>btn-ghost</c>, text box → <c>input</c>, grid → <c>table</c> …), including controls added
    /// later. A control's own classes are added on top (<c>flex-1</c>, <c>mt-2</c>, <c>bg-red-50</c> keep the automatic look);
    /// when they name a component themselves (<c>btn-primary</c>, <c>card</c> …), the automatic one is not used. Automatic classes
    /// are not written to Designer.cs. Combined with a <see cref="TwPreset"/>, an existing form gets a consistent look without
    /// writing classes.
    /// </summary>
    [Category("FormWind"), DefaultValue(false), Description("Gives the controls the theme's component for their type.")]
    public bool AutoStyle
    {
        get => _autoStyle;
        set
        {
            if (_autoStyle == value) return;
            _autoStyle = value;
            UpdateAutoStyle();
        }
    }

    /// <summary>
    /// With <see cref="AutoStyle"/>, controls whose background is their parent's background set explicitly are left to inherit
    /// it before they are styled (see <see cref="InheritCopiedBackColors"/>). Theme engines of existing applications often copy
    /// the form's background into every control; without this, a label that only gets a text color keeps the application's
    /// background when the form gets a new one (dark text on the old dark background). Not done in the Designer.
    /// </summary>
    [Category("FormWind"), DefaultValue(true), Description("With AutoStyle, controls that copy their parent's background follow the parent's new background.")]
    public bool FollowParentBackColor { get; set; } = true;

    /// <summary>
    /// Leaves to inheritance the <c>BackColor</c> of the descendants of <paramref name="root"/> whose explicitly set background
    /// equals their parent's, comparing the colors as they are before any change, so they follow the parent's background
    /// from then on. Call it before the controls are styled. Controls whose default background differs from the parent's
    /// (e.g. <see cref="TextBox"/>: <see cref="System.Drawing.SystemColors.Window"/>) keep their color. Composite controls
    /// (<see cref="PropertyGrid"/>, <see cref="UpDownBase"/>, <see cref="TwTextBox"/>) are not entered.
    /// </summary>
    /// <param name="root">The form or container whose descendants are checked.</param>
    /// <param name="filter">Returns <c>false</c> for controls to leave alone; <c>null</c> checks all.</param>
    /// <returns>The number of controls left to inheritance.</returns>
    public static int InheritCopiedBackColors(Control root, Func<Control, bool>? filter = null)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        return InheritCopiedBackColorsIn(root, root.BackColor.ToArgb(), filter, null);
    }

    private static int InheritCopiedBackColorsIn(Control parent, int parentBack, Func<Control, bool>? filter, HashSet<Control>? visited)
    {
        int count = 0;
        foreach (Control child in parent.Controls)
        {
            // Torunlar çocuğun özgün zeminiyle karşılaştırılır: çocuk önce bırakılsaydı ebeveyninin rengini gösterirdi.
            var before = child.BackColor;
            if (IsAutoContainer(child) && (visited is null || visited.Add(child)))
                count += InheritCopiedBackColorsIn(child, before.ToArgb(), filter, visited);
            if (InheritIfCopied(child, parentBack, filter)) count++;
        }
        return count;
    }

    private static bool InheritIfCopied(Control control, int parentBack, Func<Control, bool>? filter)
    {
        var before = control.BackColor;
        if (before.ToArgb() != parentBack || !TwOriginal.IsExplicit(control, nameof(Control.BackColor))) return false;
        if (filter != null && !filter(control)) return false;
        control.ResetBackColor();
        // Ebeveynin o anki zemini: sonradan eklenen kontrolde ebeveyn çoktan stillenmiştir.
        if (control.Parent is Control parent && control.BackColor.ToArgb() == parent.BackColor.ToArgb()) return true;
        control.BackColor = before;
        return false;
    }

    // FormWind'in zemin kanalını izlediği kontrollere dokunulmaz: izlenen rengin değişmesi uygulamanın ataması sayılır ve
    // class'lar o kanalı bırakırdı. Class'ları atanmış ama henüz uygulanmamış kontroller (InitializeComponent) bırakılabilir.
    private bool CanInheritBackColor(Control control) =>
        !_bindings.TryGetValue(control, out var binding) || binding.Original is null;

    private void InheritCopiedBackColorsOnce(Control container)
    {
        if (!FollowParentBackColor || IsDesignMode || !IsAutoContainer(container) || !_backColorsChecked.Add(container)) return;
        InheritCopiedBackColorsIn(container, container.BackColor.ToArgb(), CanInheritBackColor, _backColorsChecked);
    }

    /// <summary>The form (or user control) whose controls <see cref="AutoStyle"/> styles. The Designer sets it to the form being designed.</summary>
    [Category("FormWind"), DefaultValue(null), Description("The form whose controls AutoStyle styles.")]
    public ContainerControl? ContainerControl
    {
        get => _containerControl;
        set
        {
            if (_containerControl == value) return;
            ClearAutoStyle();
            _containerControl = value;
            UpdateAutoStyle();
        }
    }

    /// <summary>When the provider is added to a form in the Designer, <see cref="ContainerControl"/> becomes that form (as with ErrorProvider).</summary>
    public override ISite? Site
    {
        get => base.Site;
        set
        {
            base.Site = value;
            if (value?.GetService(typeof(IDesignerHost)) is IDesignerHost { RootComponent: ContainerControl root })
                ContainerControl = root;
        }
    }

    public bool CanExtend(object extendee) => extendee is Control || extendee is DataGridViewColumn;

    [Category("FormWind")]
    [DefaultValue("")]
    [Description("Column classes: cells (text-right font-semibold px-3), header (header:…), w-32, min-w-20, flex-1, hidden.")]
    [Editor(typeof(ClassesEditor), typeof(UITypeEditor))]
    [TypeConverter(typeof(ClassesConverter))]
    public string GetColumnClasses(DataGridViewColumn column) =>
        column != null && _columns.TryGetValue(column, out var binding) ? binding.Classes : "";

    public void SetColumnClasses(DataGridViewColumn column, string? value)
    {
        if (column is null) throw new ArgumentNullException(nameof(column));
        var classes = TwParser.Normalize(value);
        if (!_columns.TryGetValue(column, out var binding))
        {
            if (classes.Length == 0) return;
            binding = new TwColumnBinding(this, column);
            _columns.Add(column, binding);
        }
        binding.SetClasses(classes, Theme);
        if (!_initializing) binding.Refresh();
    }

    /// <summary>
    /// Conditional styles per row. The rule returns classes for each row (null = no change); dark mode
    /// variants work too. A <c>null</c> rule removes the current rule.
    /// </summary>
    public void SetRowClasses(DataGridView grid, Func<DataGridViewRow, string?>? rule)
    {
        if (grid is null) throw new ArgumentNullException(nameof(grid));
        if (_rowRules.TryGetValue(grid, out var existing))
        {
            existing.Detach();
            _rowRules.Remove(grid);
        }
        if (rule != null) _rowRules[grid] = new TwRowRule(this, grid, rule);
    }

    internal void RemoveColumn(DataGridViewColumn column) => _columns.Remove(column);

    [Category("FormWind")]
    [DefaultValue("")]
    [Description("Tailwind-style utility classes, e.g. bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700")]
    [Editor(typeof(ClassesEditor), typeof(UITypeEditor))]
    [TypeConverter(typeof(ClassesConverter))]
    public string GetClasses(Control control) =>
        control != null && _bindings.TryGetValue(control, out var binding) ? binding.OwnClasses : "";

    public void SetClasses(Control control, string? value)
    {
        if (control is null) throw new ArgumentNullException(nameof(control));

        var own = TwParser.Normalize(value);
        _bindings.TryGetValue(control, out var binding);
        Update(control, binding, own, IsAutoStyled(control) ? AutomaticFor(control, own) : null);
    }

    // Uygulanan class'lar: otomatik bileşen (varsa) ve üzerine kontrolün kendi class'ları.
    private void Update(Control control, TwBinding? binding, string own, string? automatic)
    {
        var classes = automatic is null ? own : own.Length == 0 ? automatic : automatic + " " + own;
        if (binding is null)
        {
            if (classes.Length == 0) return;
            binding = new TwBinding(this, control);
            _bindings.Add(control, binding);
        }

        binding.OwnClasses = own;
        binding.AutomaticClasses = automatic;
        binding.SetClasses(classes, Theme);
        if (!_initializing) binding.Refresh(force: true);
    }

    // Kontrolün tipine eşlenen bileşen; kendi class'larında bir bileşen adı varsa kullanılmaz.
    private string? AutomaticFor(Control control, string own)
    {
        var theme = Theme;
        if (TwClassMerge.Split(own).Any(token => theme.GetComponent(token) != null)) return null;
        return theme.GetAutoStyle(control.GetType());
    }

    /// <summary>The component <see cref="AutoStyle"/> gives the control (under its own classes); <c>null</c> when none.</summary>
    internal string? GetAutomaticClasses(Control control) =>
        _bindings.TryGetValue(control, out var binding) ? binding.AutomaticClasses : null;

    // Kontrol AutoStyle'ın kapsamında mı: konteynerin kendisi ya da gezilen bir konteynerin çocuğu.
    private bool IsAutoStyled(Control control) =>
        _autoStyle && _containerControl != null
        && (control == _containerControl || (control.Parent != null && _autoContainers.Contains(control.Parent)));

    private void UpdateAutoStyle()
    {
        if (_initializing) return;
        if (_autoStyle && _containerControl != null) ApplyAutoStyle(_containerControl);
        else ClearAutoStyle();
    }

    private void ApplyAutoStyle(Control control)
    {
        if (control.IsDisposed) return;
        InheritCopiedBackColorsOnce(control);
        ApplyAutomaticClasses(control);
        if (!IsAutoContainer(control)) return;
        if (_autoContainers.Add(control))
        {
            control.ControlAdded += OnAutoControlAdded;
            control.Disposed += OnAutoContainerDisposed;
        }
        foreach (Control child in control.Controls) ApplyAutoStyle(child);
    }

    private void ApplyAutomaticClasses(Control control)
    {
        _bindings.TryGetValue(control, out var binding);
        var own = binding?.OwnClasses ?? "";
        var automatic = AutomaticFor(control, own);
        if (binding is null && automatic is null) return;
        Update(control, binding, own, automatic);
    }

    // Kullanıcının yerleştirdiği kontrolleri taşıyanlar: form, kullanıcı kontrolü, panel, grup, sekmeler, bölücü. İç kontrolleri
    // olan bileşik kontrollere (TwTextBox'ın metin kutusu, NumericUpDown, PropertyGrid) girilmez.
    private static bool IsAutoContainer(Control control) =>
        control is not TwTextBox && control is not UpDownBase && control is not PropertyGrid
        && (control is ContainerControl || control is Panel || control is GroupBox || control is TabControl || control is TwTabControl);

    private void ClearAutoStyle()
    {
        foreach (var binding in _bindings.Values.Where(b => b.AutomaticClasses != null).ToList())
            Update(binding.Control, binding, binding.OwnClasses, null);
        UnhookAutoContainers();
    }

    private void UnhookAutoContainers()
    {
        foreach (var container in _autoContainers)
        {
            container.ControlAdded -= OnAutoControlAdded;
            container.Disposed -= OnAutoContainerDisposed;
        }
        _autoContainers.Clear();
        _backColorsChecked.Clear();
    }

    private void OnAutoControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control is null || !_autoStyle) return;
        // Ebeveyn zaten stillendi; sonradan eklenen kontrol ebeveynin özgün zeminiyle karşılaştırılır.
        if (FollowParentBackColor && !IsDesignMode && sender is Control parent
            && _bindings.TryGetValue(parent, out var binding) && binding.Original?.BackColor is System.Drawing.Color original)
            InheritIfCopied(e.Control, original.ToArgb(), CanInheritBackColor);
        ApplyAutoStyle(e.Control);
    }

    private void OnAutoContainerDisposed(object? sender, EventArgs e)
    {
        if (sender is not Control container) return;
        container.ControlAdded -= OnAutoControlAdded;
        container.Disposed -= OnAutoContainerDisposed;
        _autoContainers.Remove(container);
        _backColorsChecked.Remove(container);
    }

    internal TwParsed? GetParsed(Control control) =>
        _bindings.TryGetValue(control, out var binding) ? binding.Parsed : null;

    /// <summary>
    /// The colors <paramref name="classes"/> give in the provider's current mode (<see cref="DarkMode"/>, high contrast), for
    /// code that paints controls itself (a <c>CellPainting</c> handler, an owner-drawn ListView …). Theme components are
    /// expanded: <c>GetColors("list", "selected")</c> is the selected item of the <c>list</c> component.
    /// </summary>
    /// <param name="classes">Class string, e.g. <c>"bg-white text-gray-900 dark:bg-gray-800 selected:bg-primary-600"</c>.</param>
    /// <param name="part">A part such as <c>selected</c>, <c>header</c>, <c>item</c>; <c>null</c> for the base classes.</param>
    /// <param name="state">Extra states, e.g. <see cref="TwState.Hover"/> for the item under the cursor.</param>
    public TwColors GetColors(string classes, string? part = null, TwState state = TwState.None) =>
        new TwColors(Theme.Parse(TwParser.Normalize(classes)).ResolvePart(part, BaseState | state));

    /// <summary>
    /// The colors of <paramref name="control"/>'s classes (automatic ones included) in its current state (dark, hover, focus,
    /// disabled …); all <c>null</c> when the control has no classes. See <see cref="GetColors(string, string?, TwState)"/>.
    /// </summary>
    public TwColors GetCurrentColors(Control control, string? part = null, TwState state = TwState.None)
    {
        if (control is null) throw new ArgumentNullException(nameof(control));
        if (!_bindings.TryGetValue(control, out var binding)) return new TwColors(new TwStyle());
        return new TwColors(binding.Parsed.ResolvePart(part, binding.State | state));
    }

    /// <summary>Recalculates the control's style (e.g. after application code changed a property).</summary>
    public void Refresh(Control control)
    {
        if (control != null && _bindings.TryGetValue(control, out var binding))
            binding.Refresh(force: true);
    }

    private readonly Dictionary<Control, string> _errors = new Dictionary<Control, string>();
    private readonly HashSet<Control> _clearOnEdit = new HashSet<Control>();

    /// <summary>
    /// Sets the control's validation error (<c>null</c>/empty clears it). While there is an error the <c>invalid:</c> variant is
    /// applied; if the control is inside a <see cref="TwField"/>, the message is shown below the field. The error clears itself
    /// when the user changes the value.
    /// </summary>
    public void SetError(Control control, string? message)
    {
        if (control is null) throw new ArgumentNullException(nameof(control));
        var editor = control is TwField field ? field.Editor ?? control : control;
        if (string.IsNullOrWhiteSpace(message)) _errors.Remove(editor);
        else _errors[editor] = message!;

        if (editor.Parent is TwField owner) owner.ErrorText = message;
        if (_clearOnEdit.Add(editor))
        {
            void Clear(object? sender, EventArgs e)
            {
                if (_errors.ContainsKey(editor)) SetError(editor, null);
            }
            editor.TextChanged += Clear;
            if (editor is TwSelect select) select.SelectedIndexChanged += Clear;
            if (editor is TwDatePicker picker) picker.ValueChanged += Clear;
            if (editor is CheckBox check) check.CheckedChanged += Clear;
            editor.Disposed += (_, _) =>
            {
                _errors.Remove(editor);
                _clearOnEdit.Remove(editor);
            };
        }

        Refresh(editor);
        if (editor.Parent is TwField parentField) Refresh(parentField);
        ErrorsChanged?.Invoke(this, EventArgs.Empty);
    }

    public string? GetError(Control control)
    {
        var editor = control is TwField field ? field.Editor ?? control : control;
        return _errors.TryGetValue(editor, out var message) ? message : null;
    }

    [Browsable(false)]
    public bool HasErrors => _errors.Count > 0;

    /// <summary>When an error is added or cleared.</summary>
    public event EventHandler? ErrorsChanged;

    public void ClearErrors()
    {
        foreach (var control in _errors.Keys.ToList()) SetError(control, null);
    }

    /// <summary>
    /// Checks the rules of every <see cref="TwField"/> in <paramref name="container"/>, shows the errors and focuses the first
    /// invalid field. <c>true</c> when all are valid.
    /// </summary>
    public bool Validate(Control container)
    {
        TwField? first = null;
        foreach (var field in Descendants(container).OfType<TwField>())
        {
            var message = field.Validate();
            SetError(field, message);
            if (message != null && first is null) first = field;
        }
        (first?.Editor ?? first)?.Focus();
        return first is null;
    }

    private void RefreshBreakpoints(Form form)
    {
        var affected = _bindings.Values.Where(b => b.UsesBreakpoints && b.Control.FindForm() == form).ToList();
        if (affected.Count == 0) return;
        form.SuspendLayout();
        try
        {
            foreach (var binding in affected) binding.Refresh(force: true);
        }
        finally
        {
            form.ResumeLayout(true);
        }
        BreakpointChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>When a form's width breakpoint changes (after the sm:/md:… variants have been reapplied).</summary>
    public event EventHandler? BreakpointChanged;

    internal bool HasError(Control control) =>
        _errors.ContainsKey(control) || (control is TwField field && field.Editor is Control editor && _errors.ContainsKey(editor));

    private static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private bool _dpiRefreshPending;
    private readonly HashSet<Form> _hookedForms = new HashSet<Form>();

    internal void HookForm(Control control)
    {
        var form = control as Form ?? control.FindForm();
        if (form is null || !_hookedForms.Add(form)) return;
        form.DpiChanged += (_, _) => ScheduleDpiRefresh(form);
        // Genişlik eşiği değişince sm:/md:… varyantlı kontroller yeniden uygulanır.
        var last = TwBreakpoints.For(form);
        form.Resize += (_, _) =>
        {
            var current = TwBreakpoints.For(form);
            if (current == last) return;
            last = current;
            RefreshBreakpoints(form);
        };
        form.Disposed += (_, _) => _hookedForms.Remove(form);
    }

    /// <returns>Whether a refresh of all controls is pending (false: there is no handle to post it to).</returns>
    internal bool ScheduleDpiRefresh(Control control)
    {
        if (_dpiRefreshPending) return true;
        var target = control.FindForm() ?? control;
        if (!target.IsHandleCreated) return false;
        _dpiRefreshPending = true;
        target.BeginInvoke((Action)(() =>
        {
            _dpiRefreshPending = false;
            RefreshAll();
        }));
        return true;
    }

    public void RefreshAll()
    {
        if (_initializing) return;

        // Font ve padding değişiklikleri her seferinde ebeveynin yerleşimini tetikler (kontrol sayısıyla karesel);
        // toplu güncellemede yerleşim bir kez, sonda yapılır.
        var bindings = _bindings.Values.ToList();
        var parents = bindings
            .Select(b => b.Control.Parent)
            .Where(p => p != null)
            .Distinct()
            .ToList();

        foreach (var parent in parents) parent!.SuspendLayout();
        try
        {
            foreach (var binding in bindings)
                binding.Refresh(force: true);
            foreach (var column in _columns.Values.ToList())
                column.Refresh();
            foreach (var grid in _rowRules.Keys)
                grid.Invalidate();
        }
        finally
        {
            foreach (var parent in parents) parent!.ResumeLayout(true);
        }
    }

    public void BeginInit() => _initializing = true;

    public void EndInit()
    {
        // Otomatik class'lar başlatma sürerken atanır; hepsi aşağıda bir kez uygulanır.
        if (_autoStyle && _containerControl != null) ApplyAutoStyle(_containerControl);
        _initializing = false;
        RefreshAll();
    }

    internal bool ShouldApply => !DesignMode || _designTimePreview;

    internal void StartHoverTracking()
    {
        if (!_hoverTimer.Enabled) _hoverTimer.Start();
    }

    internal void Remove(Control control) => _bindings.Remove(control);

    internal bool IsInHoveredGroup(Control control)
    {
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
        {
            if (_bindings.TryGetValue(parent, out var binding) && binding.IsGroup && binding.Hovered) return true;
        }
        return false;
    }

    internal void RefreshGroupDescendants(Control group)
    {
        foreach (var binding in _bindings.Values.ToList())
        {
            if (binding.Control != group && group.Contains(binding.Control)) binding.Refresh();
        }
    }

    internal void ReportError(object target, string classes, Exception exception)
    {
        var handler = StyleError;
        if (handler != null)
        {
            handler(this, new TwStyleErrorEventArgs(target, classes, exception));
            return;
        }
        var name = (target as Control)?.Name ?? (target as DataGridViewColumn)?.Name ?? target.GetType().Name;
        Debug.WriteLine($"FormWind: error while styling '{name}' ({classes}): {exception}");
    }

    internal void ReportUnknown(Control control, TwParsed parsed)
    {
        var handler = UnknownClasses;
        if (handler != null)
        {
            handler(this, new TwUnknownClassesEventArgs(control, parsed.Unknown));
            return;
        }
        Debug.WriteLine($"FormWind: unknown classes on '{control.Name}': {string.Join(", ", parsed.Unknown)}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            TwTheme.CurrentChanged -= OnCurrentThemeChanged;
            TwHighContrast.Changed -= OnHighContrastChanged;
            TwTextScale.Changed -= OnTextScaleChanged;
            TwNativeToolTips.SetDark(this, false);
            _hoverTimer.Dispose();
            UnhookAutoContainers();
            foreach (var binding in _bindings.Values.ToList())
                binding.Detach();
            _bindings.Clear();
            foreach (var rule in _rowRules.Values) rule.Detach();
            _rowRules.Clear();
            _columns.Clear();
        }
        base.Dispose(disposing);
    }

    // MouseLeave, imleç alt kontrol üzerinden dışarı çıktığında tetiklenmez; hover durumu burada kapatılır.
    private void OnHoverTick(object? sender, EventArgs e)
    {
        bool anyHovered = false;
        foreach (var binding in _bindings.Values.ToList())
        {
            if (!binding.Hovered) continue;
            if (binding.IsCursorInside()) anyHovered = true;
            else binding.SetHovered(false);
        }
        if (!anyHovered) _hoverTimer.Stop();
    }

    private void OnCurrentThemeChanged(object? sender, EventArgs e)
    {
        if (_theme is null) ReparseAll();
    }

    private void ReparseAll()
    {
        var theme = Theme;
        foreach (var binding in _bindings.Values.ToList())
            binding.Reparse(theme);
        foreach (var column in _columns.Values.ToList())
            column.Reparse(theme);
        // Otomatik class'lar yeni temanın eşlemesinden yeniden alınır (yeni eşlenen tipler de stillenir); uygulama aşağıda.
        var initializing = _initializing;
        _initializing = true;
        try
        {
            if (_autoStyle && _containerControl != null) ApplyAutoStyle(_containerControl);
        }
        finally
        {
            _initializing = initializing;
        }
        RefreshAll();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class TwUnknownClassesEventArgs : EventArgs
{
    public TwUnknownClassesEventArgs(Control control, IReadOnlyList<string> unknown)
    {
        Control = control;
        Unknown = unknown;
    }

    public Control Control { get; }

    public IReadOnlyList<string> Unknown { get; }
}
