using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Holds a control's classes, state and event subscriptions.</summary>
internal sealed class TwBinding
{
    private readonly TailwindProvider _owner;
    private readonly Control _control;
    private TwParsed _parsed = TwParsed.Empty;
    private TwOriginal? _original;
    private bool _hovered;
    private bool _pressed;
    private bool _reappliedOnHandle;
    private readonly HashSet<Control> _hookedDescendants = new HashSet<Control>();

    private TwParsed? _lastParsed;
    // Stil uygulanırken gelen yenileme (bkz. Refresh).
    private bool _applying;
    private bool _pending;
    private TwState _lastState;
    private bool _focusVisible;
    private float _lastScale;

    public TwBinding(TailwindProvider owner, Control control)
    {
        _owner = owner;
        _control = control;

        _control.MouseEnter += OnMouseEnter;
        _control.MouseLeave += OnMouseLeave;
        _control.MouseDown += OnMouseDown;
        _control.MouseUp += OnMouseUp;
        _control.Enter += OnEnter;
        _control.Leave += OnStateChanged;
        _control.EnabledChanged += OnStateChanged;
        _control.RightToLeftChanged += OnStateChanged;
        _control.BackColorChanged += OnBackColorChanged;
        _control.ForeColorChanged += OnForeColorChanged;
        _control.DpiChangedAfterParent += OnDpiChanged;
        _control.Disposed += OnDisposed;
        // Ekran değişimi formun DpiChanged olayıyla güvenilir biçimde gelir (alt kontrollerin olayları .NET Framework'te
        // her zaman tetiklenmez); kontrolün formu bulunduğunda sağlayıcı ona abone olur.
        _control.ParentChanged += (_, _) => _owner.HookForm(_control);
        // InitializeComponent'teki SetClasses ölçeklenmiş değerleri atar; formun AutoScale'i (ResumeLayout'ta) aynı padding,
        // margin ve boyutları bir kez daha ölçekler (%125'te 8 px → 10 → 12). Handle, ölçeklemeden sonra ve Load'dan önce
        // oluşur: class'lar burada yeniden uygulanınca değerler doğruya döner, Load'daki atamalar da sonra gelir.
        // Yalnızca ilk handle'da: bazı atamalar (örn. ComboBox'ın DrawMode'u) handle'ı yeniden oluşturur ve her
        // oluşumda yeniden uygulamak sonsuz döngü olurdu.
        _control.HandleCreated += (_, _) =>
        {
            _owner.HookForm(_control);
            if (_reappliedOnHandle) return;
            _reappliedOnHandle = true;
            Refresh(force: true);
        };
        _owner.HookForm(_control);
        if (_control is Form form) form.DpiChanged += OnDpiChanged;
        if (_control is CheckBox checkBox) checkBox.CheckedChanged += OnStateChanged;
        if (_control is RadioButton radioButton) radioButton.CheckedChanged += OnStateChanged;

        // Class'lar imleç zaten kontrolün üzerindeyken atanırsa (örn. tıklayınca class'ı değişen buton) MouseEnter
        // tekrar gelmez; hover durumu baştan doğru başlar.
        if (IsCursorInside())
        {
            _hovered = true;
            _owner.StartHoverTracking();
        }
    }

    public Control Control => _control;

    /// <summary>The values captured before the first application; <c>null</c> until the classes are applied once.</summary>
    public TwOriginal? Original => _original;

    public TwParsed Parsed => _parsed;

    /// <summary>Whether the classes have <c>sm:</c>/<c>md:</c>… variants (reapplied when the form is resized).</summary>
    public bool UsesBreakpoints => _parsed.UsesAny(TwState.Breakpoints);

    public string Classes { get; private set; } = "";

    /// <summary>The control's own classes (its Classes property); <see cref="Classes"/> are these under the automatic ones.</summary>
    public string OwnClasses { get; set; } = "";

    /// <summary>The component <see cref="TailwindProvider.AutoStyle"/> gives the control; <c>null</c> when none.</summary>
    public string? AutomaticClasses { get; set; }

    public bool Hovered => _hovered;

    /// <summary>Has the <c>group</c> class: the <c>group-hover:</c> variants of descendants follow this control's hover.</summary>
    public bool IsGroup { get; private set; }

    public void SetClasses(string classes, TwTheme theme)
    {
        if (classes != Classes) WinFormsApplier.ResetColorOverrides(_control);
        Classes = classes;
        Reparse(theme);
    }

    public void Reparse(TwTheme theme)
    {
        _parsed = theme.Parse(Classes);
        if (_parsed.Unknown.Count > 0) _owner.ReportUnknown(_control, _parsed);

        IsGroup = _parsed.Resolve(TwState.None).IsGroup == true;
        // İmleç bir konteynere doğrudan çocuğunun üzerinden girerse konteynerin MouseEnter'ı tetiklenmez;
        // hover'a tepki veren konteynerler bu yüzden çocuklarının MouseEnter'ını da dinler.
        if (IsGroup || _parsed.HasCondition(TwState.Hover | TwState.Active)) HookDescendants(_control);
        else UnhookDescendants();
    }

    private Action? _reapply;

    public void Refresh(bool force = false)
    {
        if (_control.IsDisposed || !_owner.ShouldApply) return;

        // Bazı özellikler (TextBox.TextAlign, BorderStyle) handle'ı yeniden oluşturur; bu sırada odak olayları yenileme
        // ister. Stil iç içe uygulanırsa, yok edilmekte olan pencere için ikinci bir yeniden oluşturma başlar ve WinForms
        // yönetilen nesnesi olmayan bir pencere bırakır (çöp toplamadan sonra süreç düşer). Yenileme uygulama bitince yapılır.
        if (_applying)
        {
            _pending = true;
            return;
        }

        _original ??= TwOriginal.Capture(_control, _owner.IsDesignMode);

        var state = CurrentState();
        // Koyu temada çevrilen grid (TwGrid.EnableDarkInversion) açık stillerle çizilir.
        if (_control is DataGridView grid) state = TwGridInversion.Update(grid, state, _reapply ??= () => Refresh(force: true));
        var scale = _owner.ScaleWithDpi ? TwDpi.DeviceDpi(_control) / 96f : 1f;
        if (!force && ReferenceEquals(_parsed, _lastParsed) && state == _lastState && scale == _lastScale) return;

        _lastParsed = _parsed;
        _lastState = state;
        _lastScale = scale;

        var style = _parsed.Resolve(state);
        var context = new TwApplyContext(_parsed, state, scale, _owner.Theme, _original, _owner.IsDesignMode)
        {
            DarkScrollbars = _owner.DarkScrollbars,
        };
        _applying = true;
        try
        {
            TwAppliers.Find(_control).Apply(_control, style, context);
        }
        catch (Exception ex) when (!TwErrors.IsCritical(ex))
        {
            // Bir sonraki durum değişikliğinde yeniden denensin.
            _lastParsed = null;
            _owner.ReportError(_control, Classes, ex);
        }
        finally
        {
            _applying = false;
        }

        // Handle yeniden oluşturulurken uygulayıcı handle'ı yeniden oluşturan özellikleri atlar; sonraki yenileme tam uygulasın.
        if (_control.RecreatingHandle) _lastParsed = null;
        if (_pending)
        {
            _pending = false;
            Refresh(force: true);
        }
    }

    // Uygulama kendi aldığı rengi varsayılana döndürdü (ResetBackColor): class'ların rengi hemen geri gelir.
    private void OnBackColorChanged(object? sender, EventArgs e)
    {
        if (!_applying && WinFormsApplier.IsColorReleased(_control, back: true, _original)) Refresh(force: true);
    }

    private void OnForeColorChanged(object? sender, EventArgs e)
    {
        if (!_applying && WinFormsApplier.IsColorReleased(_control, back: false, _original)) Refresh(force: true);
    }

    /// <summary>
    /// Puts back the values the control had before the classes, keeping the classes (Designer: the preview is turned off, or
    /// the control is being written to Designer.cs). <see cref="Refresh"/> with <c>force</c> applies them again.
    /// </summary>
    public void Restore()
    {
        if (_original is null || _applying || _control.IsDisposed) return;
        var empty = _owner.Theme.Parse("");
        var context = new TwApplyContext(empty, _lastState, _lastScale > 0 ? _lastScale : 1f, _owner.Theme, _original, _owner.IsDesignMode)
        {
            DarkScrollbars = _owner.DarkScrollbars,
        };
        _applying = true;
        try
        {
            TwAppliers.Find(_control).Apply(_control, empty.Resolve(_lastState), context);
        }
        catch (Exception ex) when (!TwErrors.IsCritical(ex))
        {
            _owner.ReportError(_control, Classes, ex);
        }
        finally
        {
            _applying = false;
            _lastParsed = null;
        }
    }

    public bool IsCursorInside() =>
        _control.IsHandleCreated
        && _control.Visible
        && _control.ClientRectangle.Contains(_control.PointToClient(Control.MousePosition));

    public void SetHovered(bool hovered)
    {
        if (_hovered == hovered) return;
        _hovered = hovered;
        if (!hovered) _pressed = false;
        Refresh();
        if (IsGroup) _owner.RefreshGroupDescendants(_control);
    }

    public void Detach()
    {
        _control.MouseEnter -= OnMouseEnter;
        _control.MouseLeave -= OnMouseLeave;
        _control.MouseDown -= OnMouseDown;
        _control.MouseUp -= OnMouseUp;
        _control.Enter -= OnEnter;
        _control.Leave -= OnStateChanged;
        _control.EnabledChanged -= OnStateChanged;
        _control.RightToLeftChanged -= OnStateChanged;
        _control.BackColorChanged -= OnBackColorChanged;
        _control.ForeColorChanged -= OnForeColorChanged;
        _control.DpiChangedAfterParent -= OnDpiChanged;
        _control.Disposed -= OnDisposed;
        if (_control is CheckBox checkBox) checkBox.CheckedChanged -= OnStateChanged;
        if (_control is RadioButton radioButton) radioButton.CheckedChanged -= OnStateChanged;
        UnhookDescendants();
    }

    private void HookDescendants(Control parent)
    {
        if (parent != _control && !_hookedDescendants.Add(parent)) return;
        if (parent != _control) parent.MouseEnter += OnDescendantMouseEnter;
        parent.ControlAdded -= OnDescendantAdded;
        parent.ControlAdded += OnDescendantAdded;
        foreach (Control child in parent.Controls) HookDescendants(child);
    }

    private void UnhookDescendants()
    {
        foreach (var descendant in _hookedDescendants)
        {
            descendant.MouseEnter -= OnDescendantMouseEnter;
            descendant.ControlAdded -= OnDescendantAdded;
        }
        _hookedDescendants.Clear();
        _control.ControlAdded -= OnDescendantAdded;
    }

    private void OnDescendantAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control != null) HookDescendants(e.Control);
    }

    private void OnDescendantMouseEnter(object? sender, EventArgs e) => OnMouseEnter(sender, e);

    // Odağın klavyeyle gelip gelmediği girişte kaydedilir (focus-visible:).
    private void OnEnter(object? sender, EventArgs e)
    {
        _focusVisible = TwFocusVisible.LastInputWasKeyboard;
        OnStateChanged(sender, e);
    }

    /// <summary>The control's current state (dark, hover, focus, disabled …) as the classes are resolved with it.</summary>
    public TwState State => CurrentState();

    private TwState CurrentState()
    {
        var state = _owner.BaseState | (_control.RightToLeft == RightToLeft.Yes ? TwState.Rtl : TwState.Ltr);
        if (_control is CheckBox { Checked: true } || _control is RadioButton { Checked: true })
            state |= TwState.Checked;

        if (_owner.HasError(_control)) state |= TwState.Invalid;
        if (UsesBreakpoints) state |= TwBreakpoints.For(_control);

        // Devre dışı kontrolde etkileşim durumları uygulanmaz.
        if (!_control.Enabled) return state | TwState.Disabled;

        if (_control.ContainsFocus)
        {
            state |= TwState.Focus;
            if (_focusVisible) state |= TwState.FocusVisible;
        }
        if (_owner.IsInHoveredGroup(_control)) state |= TwState.GroupHover;
        // hover:/active: varyantı yoksa fare durumu stili değiştirmez; her girip çıkışta yeniden uygulanmaz
        // (ToolStrip gibi kontrollerde bu, fare üzerindeyken yeniden yerleşim ve takılı kalan tooltip demekti).
        if (_hovered && _parsed.HasCondition(TwState.Hover)) state |= TwState.Hover;
        if (_pressed && _parsed.HasCondition(TwState.Active)) state |= TwState.Active;
        return state;
    }

    private void OnMouseEnter(object? sender, EventArgs e)
    {
        SetHovered(true);
        _owner.StartHoverTracking();
    }

    private void OnMouseLeave(object? sender, EventArgs e)
    {
        if (!IsCursorInside()) SetHovered(false);
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _pressed = true;
        Refresh();
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (!_pressed) return;
        _pressed = false;
        Refresh();
    }

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    // DPI değişiminde WinForms fontları ve boyutları kendi sırasıyla ölçekler; class'lar bu bittikten sonra bir kez,
    // tüm kontroller için yeniden uygulanır (aksi halde bazı fontlar iki kez, bazıları hiç ölçeklenmez). Burada hemen
    // uygulanmaz: formun DpiChanged'i ölçeklemeden önce gelir; yeni DPI'a göre atanan font bir kez daha ölçeklenir,
    // ertelenen yenileme onu geri alır ve font değişimi tüm alt kontrollere yayılıp yerleşimi yeniden çalıştırır.
    // Ertelenemezse (handle yok) hemen uygulanır.
    private void OnDpiChanged(object? sender, EventArgs e)
    {
        if (!_owner.ScheduleDpiRefresh(_control)) Refresh();
    }

    private void OnDisposed(object? sender, EventArgs e)
    {
        Detach();
        _owner.Remove(_control);
    }
}
