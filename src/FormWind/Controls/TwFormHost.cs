using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Shows an existing <see cref="System.Windows.Forms.Form"/> as a page inside another window, so screens written as separate
/// windows can move into a sidebar application one by one. The form becomes a child (<c>TopLevel = false</c>, no border,
/// filling the host); its <c>Load</c> event runs when it is first shown. A form designed without automatic scaling
/// (<c>AutoScaleMode.None</c>, fixed 96 DPI sizes) is scaled once to the host's DPI (<see cref="ScaleDesignSize"/>).
/// The form's title is offered as <see cref="Title"/> and copied to <see cref="TitleTarget"/> (a <see cref="TwHeading"/> or
/// label in the toolbar), and Enter / Esc press its <c>AcceptButton</c> / <c>CancelButton</c>, which WinForms only does
/// for top-level windows. When the form closes itself (<c>Close()</c>) it is removed from the host and
/// <see cref="FormClosed"/> is raised.
/// <code>
/// var host = new TwFormHost { Dock = DockStyle.Fill, TitleTarget = toolbarTitle };
/// host.Form = new LegacyRecordsForm();
/// </code>
/// </summary>
[ToolboxItem(true)]
[Description("Hosts an existing Form as a page (TopLevel = false), scaled to the DPI, with its title moved out.")]
[DefaultEvent(nameof(FormClosed))]
public class TwFormHost : TwPanel
{
    private Form? _form;
    private Control? _titleTarget;
    private bool _scaled;

    /// <summary>The hosted form; <c>null</c> = none. Setting another form removes the previous one without disposing it.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Form? Form
    {
        get => _form;
        set
        {
            if (value == _form) return;
            if (value is { IsDisposed: true }) throw new ObjectDisposedException(value.GetType().Name);
            Detach();
            if (value != null) Attach(value);
            TitleChanged?.Invoke(this, EventArgs.Empty);
            UpdateTitleTarget();
        }
    }

    /// <summary>Scales a form with <c>AutoScaleMode.None</c> from 96 DPI to the host's DPI when it is attached; default <c>true</c>.</summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool ScaleDesignSize { get; set; } = true;

    /// <summary>The hosted form's title (<see cref="Control.Text"/>); empty when there is no form.</summary>
    [Browsable(false)]
    public string Title => _form?.Text ?? "";

    /// <summary>A control whose <see cref="Control.Text"/> follows <see cref="Title"/> (the title of a toolbar); <c>null</c> = none.</summary>
    [Category("Behavior"), DefaultValue(null)]
    public Control? TitleTarget
    {
        get => _titleTarget;
        set
        {
            _titleTarget = value;
            UpdateTitleTarget();
        }
    }

    /// <summary>The hosted form changed or its title changed.</summary>
    public event EventHandler? TitleChanged;

    /// <summary>The hosted form closed itself (it has been removed from the host).</summary>
    public event FormClosedEventHandler? FormClosed;

    private void Attach(Form form)
    {
        _form = form;
        _scaled = false;
        form.TopLevel = false;
        form.FormBorderStyle = FormBorderStyle.None;
        form.ShowInTaskbar = false;
        form.Dock = DockStyle.Fill;
        if (ScaleDesignSize && form.AutoScaleMode == AutoScaleMode.None)
        {
            float factor = TwDpi.DeviceDpi(this) / 96f;
            if (Math.Abs(factor - 1f) > 0.01f)
            {
                form.Scale(new SizeF(factor, factor));
                _scaled = true;
            }
        }
        form.TextChanged += OnFormTextChanged;
        form.FormClosed += OnFormClosed;
        Controls.Add(form);
        form.Visible = true;
        form.BringToFront();
    }

    private void Detach()
    {
        var form = _form;
        if (form is null) return;
        _form = null;
        form.TextChanged -= OnFormTextChanged;
        form.FormClosed -= OnFormClosed;
        if (!form.IsDisposed && form.Parent == this)
        {
            form.Visible = false;
            Controls.Remove(form);
        }
    }

    /// <summary>Whether the form was scaled to the DPI when it was attached (<see cref="ScaleDesignSize"/>).</summary>
    [Browsable(false)]
    public bool IsScaled => _form != null && _scaled;

    private void OnFormTextChanged(object? sender, EventArgs e)
    {
        UpdateTitleTarget();
        TitleChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        if (sender != _form) return;
        Form = null;
        FormClosed?.Invoke(this, e);
    }

    private void UpdateTitleTarget()
    {
        if (_titleTarget is { IsDisposed: false } target && target.Text != Title) target.Text = Title;
    }

    // Alt pencere olan formda AcceptButton/CancelButton çalışmaz (yalnızca üst düzey formun tuşları işlenir).
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_form is { } form && form.ContainsFocus)
        {
            if (keyData == Keys.Escape && form.CancelButton is { } cancel)
            {
                cancel.PerformClick();
                return true;
            }
            if (keyData == Keys.Enter && form.AcceptButton is { } accept && !(form.ActiveControl is TextBoxBase { Multiline: true } or Button))
            {
                accept.PerformClick();
                return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _form != null)
        {
            _form.TextChanged -= OnFormTextChanged;
            _form.FormClosed -= OnFormClosed;
        }
        base.Dispose(disposing);
    }
}
