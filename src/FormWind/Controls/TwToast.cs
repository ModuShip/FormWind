using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Where <see cref="TwToast"/> notifications appear in the owner form.</summary>
public enum TwToastPosition
{
    /// <summary>Bottom-right corner; the newest is at the bottom (the default).</summary>
    BottomRight = 0,

    /// <summary>Top-right corner; the newest is at the top.</summary>
    TopRight = 1,

    /// <summary>Bottom-left corner; the newest is at the bottom.</summary>
    BottomLeft = 2,

    /// <summary>Top-left corner; the newest is at the top.</summary>
    TopLeft = 3,

    /// <summary>Centered at the bottom; the newest is at the bottom.</summary>
    BottomCenter = 4,

    /// <summary>Centered at the top; the newest is at the top.</summary>
    TopCenter = 5,
}

/// <summary>
/// A notification that appears in the corner, does not steal focus and closes by itself.
/// <code>
/// var toast = TwToast.Show(this, "Record saved", TwDialogKind.Success, "Saved");
/// toast.Subtitle = "DR-24817 · Line 2";
/// </code>
/// Notifications stack in the owner form's corner (<see cref="Position"/>, bottom-right by default); the timer pauses while
/// hovered, a click closes it. A new one slides in from the edge while it fades in, and a success notification draws its
/// check mark.
/// </summary>
public static class TwToast
{
    private static readonly Dictionary<Form, List<TwToastForm>> Open = new Dictionary<Form, List<TwToastForm>>();

    /// <summary>Default display time (ms).</summary>
    public static int DefaultDuration { get; set; } = 4000;

    /// <summary>Where notifications appear (default <see cref="TwToastPosition.BottomRight"/>); applies to the next one shown.</summary>
    public static TwToastPosition Position { get; set; } = TwToastPosition.BottomRight;

    /// <summary>
    /// The most notifications an owner form shows at once (0 = no limit). When a new one would go over it, the oldest fades
    /// out and the new one takes its place; with 1 each notification replaces the previous.
    /// </summary>
    public static int MaxVisible { get; set; }

    /// <summary>
    /// Space (96 DPI px, scaled with the owner's DPI) kept free at the edges of the owner's client area, in addition to the
    /// 16 px margin: for example <c>new Padding(0, 56, 0, 0)</c> keeps notifications at the top below a toolbar, or
    /// <c>new Padding(0, 0, 0, 40)</c> keeps them above a status bar. Open notifications move at once.
    /// </summary>
    public static Padding Inset
    {
        get => _inset;
        set
        {
            _inset = value;
            foreach (var owner in new List<Form>(Open.Keys)) Arrange(owner);
        }
    }

    private static Padding _inset;

    /// <summary>Curve of the slide in (default <see cref="TwEasing.Out"/>; <see cref="TwEasing.Spring"/> overshoots a little and settles).</summary>
    public static TwEasing EnterEasing { get; set; } = TwEasing.Out;

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
        // Sınır aşılacaksa en eskisi listeden çıkar ve yerinde solar; yenisi onun yerine yerleşir.
        while (MaxVisible > 0 && list.Count >= MaxVisible)
        {
            var oldest = list[0];
            list.RemoveAt(0);
            oldest.Dismiss();
        }
        list.Add(toast);
        toast.SizeChanged += (_, _) => Arrange(owner);
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

    // En yenisi kenarda (altta ya da üstte); eskiler içeri doğru kayar.
    private static void Arrange(Form owner)
    {
        if (!Open.TryGetValue(owner, out var list) || owner.IsDisposed) return;
        float dpi = TwDpi.DeviceDpi(owner) / 96f;
        var client = owner.RectangleToScreen(owner.ClientRectangle);
        client = Rectangle.FromLTRB(client.Left + (int)Math.Round(_inset.Left * dpi), client.Top + (int)Math.Round(_inset.Top * dpi),
            client.Right - (int)Math.Round(_inset.Right * dpi), client.Bottom - (int)Math.Round(_inset.Bottom * dpi));
        int margin = (int)Math.Round(16 * dpi);
        bool top = Position is TwToastPosition.TopRight or TwToastPosition.TopLeft or TwToastPosition.TopCenter;
        int edge = top ? client.Top + margin : client.Bottom - margin;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var toast = list[i];
            int x = Position switch
            {
                TwToastPosition.BottomLeft or TwToastPosition.TopLeft => client.Left + margin,
                TwToastPosition.BottomCenter or TwToastPosition.TopCenter => client.Left + (client.Width - toast.Width) / 2,
                _ => client.Right - margin - toast.Width,
            };
            toast.Place(new Point(x, top ? edge : edge - toast.Height), top);
            edge += top ? toast.Height + margin / 2 : -(toast.Height + margin / 2);
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
    private Point _target;
    private bool _fromTop;
    private readonly TwCheckGlyph? _check;

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
        // Başarı bildiriminin onay işareti kendini çizer; diğerleri ikondur.
        Control glyph = kind == TwDialogKind.Success ? _check = new TwCheckGlyph() : new TwIcon { IconName = icon };
        root.Controls.Add(glyph);
        var texts = new TwPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        root.Controls.Add(texts);
        TitleLabel = new Label { AutoSize = true, Text = title ?? "", Visible = !string.IsNullOrEmpty(title) };
        MessageLabel = new Label { AutoSize = true, Text = message, MaximumSize = new Size(280, 0) };
        SubtitleLabel = new Label { AutoSize = true, MaximumSize = new Size(280, 0), Visible = false };
        texts.Controls.Add(TitleLabel);
        texts.Controls.Add(MessageLabel);
        texts.Controls.Add(SubtitleLabel);
        var close = new TwIcon { IconName = "x", Cursor = Cursors.Hand, AccessibleName = TwTexts.Current.Close, AccessibleRole = AccessibleRole.PushButton };
        close.Click += (_, _) => Dismiss();
        root.Controls.Add(close);

        _tw.SetClasses(root, "flex items-start gap-3 p-4 bg-white border border-gray-200 dark:bg-gray-800 dark:border-gray-700");
        _tw.SetClasses(glyph, $"text-lg text-{tone}-600 dark:text-{tone}-400");
        _tw.SetClasses(texts, "flex flex-col gap-0.5");
        _tw.SetClasses(TitleLabel, "text-sm font-semibold text-gray-900 dark:text-white");
        _tw.SetClasses(MessageLabel, "text-sm text-gray-600 dark:text-gray-300");
        _tw.SetClasses(SubtitleLabel, "text-xs text-gray-500 dark:text-gray-400");
        _tw.SetClasses(close, "text-sm text-gray-400 hover:text-gray-700 dark:hover:text-gray-200");
        _tw.DarkMode = dark;
        ResumeLayout(true);

        foreach (Control control in new Control[] { root, glyph, texts, TitleLabel, MessageLabel, SubtitleLabel })
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

    /// <summary>The muted line under the message (<see cref="Subtitle"/>).</summary>
    public Label SubtitleLabel { get; }

    /// <summary>
    /// A muted line under the message, e.g. a record number and a place ("DR-24817 · Line 2"); empty = none. Setting it
    /// right after <see cref="TwToast.Show"/> grows the notification and rearranges the stack.
    /// </summary>
    public string Subtitle
    {
        get => SubtitleLabel.Text;
        set
        {
            SubtitleLabel.Text = value ?? "";
            SubtitleLabel.Visible = SubtitleLabel.Text.Length > 0;
            string title = TitleLabel.Text;
            AccessibleName = (title.Length > 0 ? title + ": " : "") + MessageLabel.Text + (SubtitleLabel.Text.Length > 0 ? " (" + SubtitleLabel.Text + ")" : "");
        }
    }

    /// <summary>Whether it is still sliding and fading in.</summary>
    internal bool Entering => !_closing && _fade.Running;

    /// <summary>The place the stack gives it (its location once it has slid in).</summary>
    internal Point TargetLocation => _target;

    /// <summary>Drawn share of the success check mark; 1 for other kinds.</summary>
    internal float CheckProgress => _check?.Progress ?? 1f;

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
        // Onay işareti bildirim neredeyse görününce çizilmeye başlar.
        _check?.Play(FadeDuration);
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

    /// <summary>Moves it to its place in the stack; while sliding in, it keeps sliding towards the new place.</summary>
    internal void Place(Point target, bool fromTop)
    {
        _target = target;
        _fromTop = fromTop;
        Location = Entering ? SlideLocation(_fade.Progress) : target;
    }

    // Giriş: kendi kenarından (alttakiler aşağıdan, üsttekiler yukarıdan) 12 px kayarak yerine gelir.
    private Point SlideLocation(float progress)
    {
        int offset = (int)Math.Round(12 * TwDpi.DeviceDpi(this) / 96f * (1 - progress));
        return new Point(_target.X, _target.Y + (_fromTop ? -offset : offset));
    }

    // Solma süreye bağlıdır (kare sayısına değil): UI iş parçacığı yavaşlasa da 150 ms'de biter. Animasyon yapılamıyorsa
    // (TwMotion kapalı, pencere görünmüyor) hemen biter.
    private void StartFade()
    {
        _fadeFrom = Opacity;
        if (_closing) _fade.Start(FadeDuration);
        else _fade.Start(FadeDuration * 2, TwToast.EnterEasing);
        if (!_fade.Running) FinishFade();
        // İlk karede de kenardan başlar (hedefte bir an görünüp geri zıplamaz).
        else if (!_closing) Location = SlideLocation(0f);
    }

    private void StepFade()
    {
        double target = _closing ? 0 : 1;
        // Taşan eğride (Spring) opaklık 1'de durur, konum biraz öteye gidip geri gelir.
        double progress = _closing ? _fade.Progress : Math.Min(1f, _fade.Progress * 2);
        Opacity = _fadeFrom + (target - _fadeFrom) * Math.Max(0, Math.Min(1, progress));
        if (!_closing) Location = SlideLocation(_fade.Progress);
    }

    private void FinishFade()
    {
        if (_closing) Close();
        else
        {
            Opacity = 1;
            Location = _target;
        }
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
