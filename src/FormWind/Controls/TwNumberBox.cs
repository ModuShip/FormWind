using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Number box: a <see cref="TwTextBox"/> with up/down buttons on the right. Holding a button repeats; ↑/↓ and the mouse wheel
/// change the value too. The text is validated on focus loss or Enter and clamped to <see cref="Minimum"/>–<see cref="Maximum"/>.
/// Styled with TwTextBox classes such as <c>input</c>.
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValueChanged))]
[DefaultProperty(nameof(Value))]
[Description("Number box with up/down buttons.")]
public class TwNumberBox : TwTextBox
{
    private readonly Timer _repeat = new Timer();
    private decimal _value;
    private decimal _minimum;
    private decimal _maximum = 100;
    private decimal _increment = 1;
    private int _decimalPlaces;
    private int _repeatDirection;
    private bool _updating;

    public TwNumberBox()
    {
        InnerTextBox.KeyDown += OnInnerKeyDown;
        InnerTextBox.Leave += (_, _) => Commit();
        InnerTextBox.MouseWheel += (_, e) => Step(Math.Sign(e.Delta));
        _repeat.Tick += (_, _) =>
        {
            _repeat.Interval = 60;
            Step(_repeatDirection);
        };
        UpdateText();
    }

    public event EventHandler? ValueChanged;

    [Category("Data"), DefaultValue(typeof(decimal), "0")]
    public decimal Value
    {
        get => _value;
        set
        {
            var clamped = Clamp(Math.Round(value, _decimalPlaces));
            if (clamped == _value)
            {
                UpdateText();
                return;
            }
            _value = clamped;
            UpdateText();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Category("Data"), DefaultValue(typeof(decimal), "0")]
    public decimal Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < value) _maximum = value;
            Value = _value;
        }
    }

    [Category("Data"), DefaultValue(typeof(decimal), "100")]
    public decimal Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            if (_minimum > value) _minimum = value;
            Value = _value;
        }
    }

    [Category("Data"), DefaultValue(typeof(decimal), "1")]
    public decimal Increment
    {
        get => _increment;
        set => _increment = Math.Abs(value);
    }

    [Category("Data"), DefaultValue(0)]
    public int DecimalPlaces
    {
        get => _decimalPlaces;
        set
        {
            _decimalPlaces = Math.Max(0, Math.Min(10, value));
            Value = _value;
        }
    }

    private int StepperWidth => (int)Math.Round(22 * TwDpi.DeviceDpi(this) / 96f);

    /// <summary>The text box is placed to the left of the buttons.</summary>
    public override Rectangle DisplayRectangle
    {
        get
        {
            var rect = base.DisplayRectangle;
            return new Rectangle(rect.X, rect.Y, Math.Max(0, rect.Width - StepperWidth), rect.Height);
        }
    }

    /// <summary>Converts the text to a value (current culture); if invalid the previous value is written back.</summary>
    public void Commit()
    {
        if (decimal.TryParse(InnerTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed)) Value = parsed;
        else UpdateText();
    }

    /// <summary>Changes the value by <paramref name="direction"/> × <see cref="Increment"/>.</summary>
    public void Step(int direction)
    {
        if (direction == 0) return;
        Commit();
        Value = _value + direction * _increment;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        PerformLayout();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var stepper = StepperBounds();
        var color = Enabled ? ForeColor : Color.FromArgb(128, ForeColor);
        var line = BorderColor.IsEmpty ? Color.FromArgb(60, color) : BorderColor;
        using (var pen = new Pen(line))
            e.Graphics.DrawLine(pen, stepper.Left, stepper.Top + 4, stepper.Left, stepper.Bottom - 4);

        int em = TwIconRenderer.EmPixels(Math.Max(6f, Font.SizeInPoints * 0.75f), TwDpi.DeviceDpi(this));
        var half = stepper.Height / 2;
        TwIconRenderer.DrawIcon(e.Graphics, "chevron-up", em, new Rectangle(stepper.X, stepper.Y, stepper.Width, half), color);
        TwIconRenderer.DrawIcon(e.Graphics, "chevron-down", em, new Rectangle(stepper.X, stepper.Y + half, stepper.Width, stepper.Height - half), color);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        var stepper = StepperBounds();
        if (e.Button == MouseButtons.Left && stepper.Contains(e.Location))
        {
            _repeatDirection = e.Y < stepper.Top + stepper.Height / 2 ? 1 : -1;
            Step(_repeatDirection);
            _repeat.Interval = 400; // ilk tekrar gecikmesi
            _repeat.Start();
            InnerTextBox.Focus();
            return;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _repeat.Stop();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = StepperBounds().Contains(e.Location) ? Cursors.Default : Cursors.IBeam;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _repeat.Dispose();
        base.Dispose(disposing);
    }

    private Rectangle StepperBounds()
    {
        var border = BorderInsets();
        return new Rectangle(Width - border.Right - StepperWidth, border.Top, StepperWidth, Math.Max(0, Height - border.Vertical));
    }

    private void OnInnerKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Up: Step(1); e.Handled = true; break;
            case Keys.Down: Step(-1); e.Handled = true; break;
            case Keys.Enter: Commit(); e.SuppressKeyPress = true; break;
        }
    }

    private decimal Clamp(decimal value) => Math.Max(_minimum, Math.Min(_maximum, value));

    private void UpdateText()
    {
        if (_updating) return;
        _updating = true;
        try
        {
            InnerTextBox.Text = _value.ToString("N" + _decimalPlaces.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture);
        }
        finally
        {
            _updating = false;
        }
    }
}
