using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// A notification that appears in the corner, does not steal focus and closes by itself.
/// <code>TwToast.Show(this, "Order saved", TwDialogKind.Success);</code>
/// Notifications stack in the owner form's bottom-right corner; the timer pauses while hovered, a click closes it.
/// </summary>
public static class TwToast
{
    private static readonly Dictionary<Form, List<TwToastForm>> Open = new Dictionary<Form, List<TwToastForm>>();

    /// <summary>Default display time (ms).</summary>
    public static int DefaultDuration { get; set; } = 4000;

    public static TwToastForm Show(Form owner, string message, TwDialogKind kind = TwDialogKind.Info, string? title = null, int? durationMs = null)
    {
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        var toast = new TwToastForm(message, title, kind, TwTitleBar.For(owner).Dark, durationMs ?? DefaultDuration) { Owner = owner };
        if (!Open.TryGetValue(owner, out var list))
        {
            Open[owner] = list = new List<TwToastForm>();
            owner.LocationChanged += OnOwnerMoved;
            owner.SizeChanged += OnOwnerMoved;
            owner.Disposed += OnOwnerDisposed;
        }
        list.Add(toast);
        toast.FormClosed += (_, _) =>
        {
            list.Remove(toast);
            // Son bildirim kapanınca sahip form bırakılır; statik sözlük formu yaşatmasın.
            if (list.Count == 0) Release(owner);
            else Arrange(owner);
        };
        Arrange(owner);
        toast.Show(owner);
        return toast;
    }

    /// <summary>The owner form's open notifications (newest last).</summary>
    public static IReadOnlyList<TwToastForm> OpenToasts(Form owner) =>
        Open.TryGetValue(owner, out var list) ? list.AsReadOnly() : (IReadOnlyList<TwToastForm>)Array.Empty<TwToastForm>();

    /// <summary>Number of owner forms with open notifications (for tests).</summary>
    internal static int TrackedOwners => Open.Count;

    private static void OnOwnerMoved(object? sender, EventArgs e) => Arrange((Form)sender!);

    private static void OnOwnerDisposed(object? sender, EventArgs e) => Release((Form)sender!);

    private static void Release(Form owner)
    {
        if (!Open.Remove(owner)) return;
        owner.LocationChanged -= OnOwnerMoved;
        owner.SizeChanged -= OnOwnerMoved;
        owner.Disposed -= OnOwnerDisposed;
    }

    // En yenisi en altta; eskiler yukarı kayar.
    private static void Arrange(Form owner)
    {
        if (!Open.TryGetValue(owner, out var list) || owner.IsDisposed) return;
        var client = owner.RectangleToScreen(owner.ClientRectangle);
        int margin = (int)Math.Round(16 * TwDpi.DeviceDpi(owner) / 96f);
        int bottom = client.Bottom - margin;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var toast = list[i];
            toast.Location = new Point(client.Right - margin - toast.Width, bottom - toast.Height);
            bottom -= toast.Height + margin / 2;
        }
    }
}

/// <summary><see cref="TwToast"/>'un penceresi.</summary>
public sealed class TwToastForm : Form
{
    private const int WsExNoActivate = 0x08000000, WsExToolWindow = 0x00000080, CsDropShadow = 0x00020000;

    private readonly TailwindProvider _tw = new TailwindProvider();
    private readonly Timer _timer = new Timer();
    private readonly TwTween _fade;
    private double _fadeFrom;
    private int _remaining;
    private bool _closing;

    /// <summary>Fade-in and fade-out time (ms).</summary>
    internal const int FadeDuration = 150;

    internal TwToastForm(string message, string? title, TwDialogKind kind, bool dark, int durationMs)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        // Animasyon kapalıyken katmanlı pencere hiç oluşmaz; bildirim tam opak açılır.
        Opacity = TwMotion.Enabled ? 0 : 1;
        AccessibleRole = AccessibleRole.Alert;
        AccessibleName = string.IsNullOrEmpty(title) ? message : title + ": " + message;
        Kind = kind;
        _remaining = durationMs;

        var (icon, tone) = kind switch
        {
            TwDialogKind.Success => ("check", "green"),
            TwDialogKind.Warning => ("warning", "amber"),
            TwDialogKind.Error => ("error", "red"),
            TwDialogKind.Question => ("help", "blue"),
            _ => ("info", "blue"),
        };

        var root = new TwPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        Controls.Add(root);
        var glyph = new TwIcon { IconName = icon };
        root.Controls.Add(glyph);
        var texts = new TwPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        root.Controls.Add(texts);
        TitleLabel = new Label { AutoSize = true, Text = title ?? "", Visible = !string.IsNullOrEmpty(title) };
        MessageLabel = new Label { AutoSize = true, Text = message, MaximumSize = new Size(280, 0) };
        texts.Controls.Add(TitleLabel);
        texts.Controls.Add(MessageLabel);
        var close = new TwIcon { IconName = "x", Cursor = Cursors.Hand, AccessibleName = TwTexts.Current.Close, AccessibleRole = AccessibleRole.PushButton };
        close.Click += (_, _) => Dismiss();
        root.Controls.Add(close);

        _tw.SetClasses(root, "flex items-start gap-3 p-4 bg-white border border-gray-200 dark:bg-gray-800 dark:border-gray-700");
        _tw.SetClasses(glyph, $"text-lg text-{tone}-600 dark:text-{tone}-400");
        _tw.SetClasses(texts, "flex flex-col gap-0.5");
        _tw.SetClasses(TitleLabel, "text-sm font-semibold text-gray-900 dark:text-white");
        _tw.SetClasses(MessageLabel, "text-sm text-gray-600 dark:text-gray-300");
        _tw.SetClasses(close, "text-sm text-gray-400 hover:text-gray-700 dark:hover:text-gray-200");
        _tw.DarkMode = dark;
        ResumeLayout(true);

        foreach (Control control in new Control[] { root, glyph, texts, TitleLabel, MessageLabel })
        {
            control.MouseEnter += (_, _) => _timer.Stop();
            control.MouseLeave += (_, _) => { if (!ClientRectangle.Contains(PointToClient(MousePosition))) StartTimer(); };
            if (control != root) control.Click += (_, _) => Dismiss();
        }
        root.Click += (_, _) => Dismiss();

        _timer.Tick += (_, _) => Dismiss();
        _fade = new TwTween(this);
        _fade.Tick += (_, _) => StepFade();
        _fade.Completed += (_, _) => FinishFade();
    }

    public TwDialogKind Kind { get; }

    public Label TitleLabel { get; }

    public Label MessageLabel { get; }

    public TailwindProvider Provider => _tw;

    /// <summary>Does not steal focus.</summary>
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExNoActivate | WsExToolWindow;
            cp.ClassStyle |= CsDropShadow;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Windows 11: yuvarlak köşe.
        int round = 2;
        try { DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int)); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (Opacity < 1) StartFade();
        StartTimer();
        AccessibilityNotifyClients(AccessibleEvents.SystemAlert, -1);
    }

    /// <summary>Fades the notification out and closes it (closes immediately when <see cref="TwMotion.Enabled"/> is off).</summary>
    public void Dismiss()
    {
        if (_closing) return;
        _closing = true;
        _timer.Stop();
        StartFade();
    }

    private void StartTimer()
    {
        if (_closing || _remaining <= 0) return;
        _timer.Interval = Math.Max(1, _remaining);
        _timer.Start();
    }

    // Solma süreye bağlıdır (kare sayısına değil): UI iş parçacığı yavaşlasa da 150 ms'de biter. Animasyon yapılamıyorsa
    // (TwMotion kapalı, pencere görünmüyor) hemen biter.
    private void StartFade()
    {
        _fadeFrom = Opacity;
        _fade.Start(FadeDuration);
        if (!_fade.Running) FinishFade();
    }

    private void StepFade()
    {
        double target = _closing ? 0 : 1;
        Opacity = _fadeFrom + (target - _fadeFrom) * _fade.Progress;
    }

    private void FinishFade()
    {
        if (_closing) Close();
        else Opacity = 1;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _fade.Dispose();
            _tw.Dispose();
        }
        base.Dispose(disposing);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
