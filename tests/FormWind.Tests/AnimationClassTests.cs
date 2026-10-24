using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>ease-*, delay-*, transition-colors/shadow, motion-safe:/motion-reduce:, animate-*, TwTween.</summary>
[Collection(UiCollection.Name)]
public class AnimationClassTests
{
    private static readonly MethodInfo OnMouseEnter =
        typeof(Control).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static void Enter(Control control) => OnMouseEnter.Invoke(control, new object[] { EventArgs.Empty });

    private static Form Host() =>
        new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, Size = new Size(400, 300) };

    // Animasyonu kapatan/açan testler değeri geri alır (bkz. TestMotion).
    private static void WithMotion(bool enabled, Action body)
    {
        var previous = TwMotion.Enabled;
        TwMotion.Enabled = enabled;
        try
        {
            body();
        }
        finally
        {
            TwMotion.Enabled = previous;
        }
    }

    private static void WaitUntil(Func<bool> condition, int milliseconds = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    [Fact]
    public void Parses_easing_delay_and_transition_properties()
    {
        var theme = new TwTheme();
        var style = theme.Parse("transition-colors duration-300 delay-[40ms] ease-in-out").Resolve(TwState.None);
        Assert.True(style.Transition);
        Assert.Equal(TwTransitionProperty.Colors, style.TransitionProperty);
        Assert.Equal(300, style.DurationMs);
        Assert.Equal(40, style.DelayMs);
        Assert.Equal(TwEasing.InOut, style.Easing);

        Assert.Equal(TwTransitionProperty.All, theme.Parse("transition").Resolve(TwState.None).TransitionProperty);
        Assert.Equal(TwTransitionProperty.All, theme.Parse("transition-all").Resolve(TwState.None).TransitionProperty);
        Assert.Equal(TwTransitionProperty.Shadow, theme.Parse("transition-shadow").Resolve(TwState.None).TransitionProperty);
        Assert.Equal(TwEasing.Linear, theme.Parse("ease-linear").Resolve(TwState.None).Easing);
        Assert.Equal(TwEasing.In, theme.Parse("ease-in").Resolve(TwState.None).Easing);
        Assert.Equal(TwEasing.Out, theme.Parse("ease-out").Resolve(TwState.None).Easing);
        Assert.Equal(TwAnimation.Spin, theme.Parse("animate-spin").Resolve(TwState.None).Animation);
        Assert.Equal(TwAnimation.Pulse, theme.Parse("animate-pulse").Resolve(TwState.None).Animation);
        Assert.Equal(TwAnimation.None, theme.Parse("animate-spin animate-none").Resolve(TwState.None).Animation);

        var none = theme.Parse("transition hover:transition-none").Resolve(TwState.Hover);
        Assert.False(none.Transition);
        Assert.Empty(theme.Parse("delay-200 ease-in-out animate-pulse motion-safe:animate-spin").Unknown);
    }

    [Theory]
    [InlineData(TwEasing.Linear, 0.5f, 0.5f)]
    [InlineData(TwEasing.In, 0.5f, 0.125f)]
    [InlineData(TwEasing.Out, 0.5f, 0.875f)]
    [InlineData(TwEasing.InOut, 0.25f, 0.0625f)]
    [InlineData(TwEasing.InOut, 0.75f, 0.9375f)]
    public void Easing_curves_follow_their_shape(TwEasing easing, float t, float expected)
    {
        Assert.Equal(expected, TwMotion.Ease(easing, t), 3);
        Assert.Equal(0f, TwMotion.Ease(easing, -1f));
        Assert.Equal(1f, TwMotion.Ease(easing, 2f));
    }

    [Fact]
    public void Transition_waits_for_the_delay_and_uses_the_curve()
    {
        var timing = new TwTransition(100, 50, TwEasing.Linear);
        Assert.Equal(0f, timing.Progress(30));
        Assert.Equal(0.5f, timing.Progress(100), 3);
        Assert.Equal(1f, timing.Progress(200));
        Assert.Equal(150, timing.TotalMs);
        Assert.True(timing.Animates(TwTransitionProperty.Colors));
        Assert.False(new TwTransition(100, properties: TwTransitionProperty.Shadow).Animates(TwTransitionProperty.Colors));
    }

    [Fact]
    public void Hover_color_waits_for_delay_then_changes_linearly() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = Host();
        var button = new TwButton { Size = new Size(100, 30) };
        form.Controls.Add(button);
        form.Show();
        try
        {
            provider.SetClasses(button, "bg-white hover:bg-black transition duration-100 delay-100 ease-linear");
            var start = DateTime.UtcNow;
            Enter(button);

            Assert.True(TwAnimator.IsAnimating(button, TwColorChannel.Back));
            TwAnimator.Step(start.AddMilliseconds(60));
            Assert.Equal(Color.White.ToArgb(), button.BackColor.ToArgb());     // gecikme sürüyor
            TwAnimator.Step(start.AddMilliseconds(150));
            Assert.InRange(button.BackColor.R, 100, 155);                       // doğrusal: yarı yolda ~%50
            TwAnimator.Step(start.AddMilliseconds(400));
            Assert.Equal(Color.Black.ToArgb(), button.BackColor.ToArgb());
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Transition_colors_animates_the_border_color() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = Host();
        var button = new TwButton { Size = new Size(100, 30) };
        form.Controls.Add(button);
        form.Show();
        try
        {
            provider.SetClasses(button, "border border-gray-200 hover:border-blue-600 transition-colors duration-200");
            Enter(button);

            Assert.True(TwAnimator.IsAnimating(button, TwColorChannel.Border));
            Assert.True(TwAnimator.TryGetTarget(button, TwColorChannel.Border, out var target));
            Assert.Equal(ColorTranslator.FromHtml("#2563eb").ToArgb(), target.ToArgb());
            TwAnimator.Step(DateTime.UtcNow.AddSeconds(1));
            Assert.False(TwAnimator.IsAnimating(button, TwColorChannel.Border));
            var chrome = (TwChrome)typeof(TwButton).GetField("_chrome", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(button)!;
            Assert.Equal(target.ToArgb(), chrome.BorderColor.ToArgb());
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Transition_colors_does_not_animate_shadows_and_transition_shadow_does_not_animate_colors() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = Host();
        var colors = new TwButton { Size = new Size(100, 30), Location = new Point(20, 20) };
        var shadow = new TwButton { Size = new Size(100, 30), Location = new Point(20, 80) };
        form.Controls.AddRange(new Control[] { colors, shadow });
        form.Show();
        try
        {
            provider.SetClasses(colors, "bg-white shadow-sm hover:bg-black hover:shadow-lg transition-colors");
            provider.SetClasses(shadow, "bg-white shadow-sm hover:bg-black hover:shadow-lg transition-shadow");
            Enter(colors);
            Enter(shadow);

            Assert.True(TwAnimator.IsAnimating(colors, TwColorChannel.Back));
            Assert.False(TwOutset.For(colors).Animating);
            Assert.False(TwAnimator.IsAnimating(shadow, TwColorChannel.Back));
            Assert.Equal(Color.Black.ToArgb(), shadow.BackColor.ToArgb());
            Assert.True(TwOutset.For(shadow).Animating);
        }
        finally
        {
            TwAnimator.Step(DateTime.UtcNow.AddSeconds(1));
            form.Close();
        }
    });

    [Fact]
    public void Focus_ring_fades_in_with_transition() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = Host();
        var box = new TwButton { Size = new Size(100, 30), Location = new Point(20, 20) };
        form.Controls.Add(box);
        form.Show();
        try
        {
            provider.SetClasses(box, "ring-0 transition");
            provider.SetClasses(box, "ring-2 transition");
            Assert.True(TwOutset.For(box).Animating);

            provider.SetClasses(box, "ring-0 transition-none");
            provider.SetClasses(box, "ring-2 transition-none");
            Assert.False(TwOutset.For(box).Animating);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Motion_variants_follow_TwMotion_and_reapply_when_it_changes() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = Host();
        var label = new Label { Text = "x" };
        form.Controls.Add(label);
        form.Show();
        WithMotion(true, () =>
        {
            provider.SetClasses(label, "bg-white motion-reduce:bg-black motion-safe:text-red-600");
            Assert.Equal(Color.White.ToArgb(), label.BackColor.ToArgb());
            Assert.Equal(ColorTranslator.FromHtml("#dc2626").ToArgb(), label.ForeColor.ToArgb());

            TwMotion.Enabled = false;                                           // Changed → yeniden uygulanır
            Assert.Equal(Color.Black.ToArgb(), label.BackColor.ToArgb());
            Assert.NotEqual(ColorTranslator.FromHtml("#dc2626").ToArgb(), label.ForeColor.ToArgb());

            TwMotion.Enabled = true;
            Assert.Equal(Color.White.ToArgb(), label.BackColor.ToArgb());
        });
        form.Close();
    });

    [Fact]
    public void Forced_colors_still_win_over_motion_variants()
    {
        var parsed = new TwTheme().Parse("bg-blue-500 motion-safe:bg-red-500 forced-colors:bg-white");
        var style = parsed.Resolve(TwState.HighContrast | TwState.MotionSafe);
        Assert.Equal(Color.White.ToArgb(), style.BackColor!.Value.ToArgb());
    }

    [Fact]
    public void Motion_changed_event_fires_only_on_change()
    {
        var previous = TwMotion.Enabled;
        int changes = 0;
        EventHandler handler = (_, _) => changes++;
        TwMotion.Changed += handler;
        try
        {
            TwMotion.Enabled = true;
            int before = changes;
            TwMotion.Enabled = true;
            Assert.Equal(before, changes);
            TwMotion.Enabled = false;
            Assert.Equal(before + 1, changes);
        }
        finally
        {
            TwMotion.Changed -= handler;
            TwMotion.Enabled = previous;
        }
    }

    [Fact]
    public void Icon_spins_and_panel_pulses_with_animate_classes() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = Host();
        var icon = new TwIcon { IconName = "sync" };
        var panel = new TwPanel { Size = new Size(80, 20), Location = new Point(0, 40) };
        form.Controls.AddRange(new Control[] { icon, panel });
        form.Show();
        try
        {
            int before = TwAnimations.Count;
            provider.SetClasses(icon, "text-base animate-spin");
            provider.SetClasses(panel, "bg-gray-200 rounded animate-pulse");
            Assert.Equal(TwAnimation.Spin, icon.Animation);
            Assert.Equal(before + 2, TwAnimations.Count);
            Assert.InRange(TwAnimations.PulseOpacity(panel), 0.5f, 1f);
            using (var bitmap = new Bitmap(icon.Width, icon.Height)) icon.DrawToBitmap(bitmap, new Rectangle(Point.Empty, icon.Size));

            WithMotion(false, () =>
            {
                Assert.Equal(0f, TwAnimations.SpinAngle(icon));                 // durağan
                Assert.Equal(1f, TwAnimations.PulseOpacity(panel));
            });

            provider.SetClasses(icon, "text-base animate-none");
            provider.SetClasses(panel, "bg-gray-200 rounded");
            Assert.Equal(TwAnimation.None, icon.Animation);
            Assert.Equal(before, TwAnimations.Count);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Switch_knob_follows_transition_none_and_duration() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = Host();
        var sw = new TwSwitch { Size = new Size(44, 24) };
        form.Controls.Add(sw);
        form.Show();
        try
        {
            provider.SetClasses(sw, "transition-none");
            sw.Checked = true;
            Assert.Equal(1f, sw.ThumbPosition);                                 // anında

            provider.SetClasses(sw, "duration-1000 ease-linear");
            sw.Checked = false;
            Thread.Sleep(200);
            Assert.InRange(sw.ThumbPosition, 0.5f, 0.95f);                      // 1 sn'lik doğrusal kayışın başı
            sw.Checked = true;                                                   // geri döner, ara konumdan başlar
            Assert.InRange(sw.ThumbPosition, 0.5f, 0.95f);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Timing_classes_replace_a_controls_default()
    {
        Assert.Equal(150, default(TwTimingClasses).For(150).DurationMs);
        var custom = new TwTimingClasses(null, 400, 20, TwEasing.InOut).For(150);
        Assert.Equal(400, custom.DurationMs);
        Assert.Equal(20, custom.DelayMs);
        Assert.Equal(TwEasing.InOut, custom.Easing);
        Assert.Equal(0, new TwTimingClasses(false, 400, null, null).For(150).TotalMs);
    }

    [Fact]
    public void Tween_waits_for_its_delay_and_completes() => Sta.Run(() =>
    {
        using var form = Host();
        form.Show();
        using var tween = new TwTween(form);
        int ticks = 0, completed = 0;
        tween.Tick += (_, _) => ticks++;
        tween.Completed += (_, _) => completed++;
        try
        {
            tween.Start(60, TwEasing.Linear, delayMs: 60);
            Assert.True(tween.Running);
            Assert.Equal(0f, tween.Progress);
            WaitUntil(() => completed > 0);
            Assert.Equal(1, completed);
            Assert.True(ticks > 0);
            Assert.Equal(1f, tween.Progress);

            WithMotion(false, () =>
            {
                tween.Start(500);
                Assert.False(tween.Running);
                Assert.Equal(1f, tween.Progress);
            });
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Dialog_and_popover_fade_in_only_when_motion_is_enabled() => Sta.Run(() =>
    {
        using var owner = Host();
        owner.Show();
        var anchor = new Button { Size = new Size(80, 24) };
        owner.Controls.Add(anchor);
        using var popover = new TwPopover { Content = new Panel { Size = new Size(120, 60) } };
        try
        {
            WithMotion(true, () =>
            {
                using var dialog = TwDialog.Create(new TwDialogOptions { Message = "Kaydedildi" }, owner);
                dialog.StartPosition = FormStartPosition.Manual;
                dialog.Location = new Point(-10000, -10000);
                dialog.Show(owner);
                Assert.True(dialog.Opacity < 1);
                WaitUntil(() => dialog.Opacity >= 1);
                Assert.Equal(1.0, dialog.Opacity);
                dialog.Close();

                popover.Show(anchor);
                Assert.True(popover.DropDown!.Opacity < 1);
                WaitUntil(() => popover.DropDown.Opacity >= 1);
                Assert.Equal(1.0, popover.DropDown.Opacity);
                popover.Close();
            });
            WithMotion(false, () =>
            {
                using var dialog = TwDialog.Create(new TwDialogOptions { Message = "Kaydedildi" }, owner);
                dialog.StartPosition = FormStartPosition.Manual;
                dialog.Location = new Point(-10000, -10000);
                dialog.Show(owner);
                Assert.Equal(1.0, dialog.Opacity);
                dialog.Close();

                popover.Show(anchor);
                Assert.Equal(1.0, popover.DropDown!.Opacity);
                popover.Close();
            });
        }
        finally
        {
            owner.Close();
        }
    });

    [Fact]
    public void Toast_fade_is_time_based() => Sta.Run(() =>
    {
        using var owner = Host();
        owner.Show();
        try
        {
            WithMotion(true, () =>
            {
                var toast = TwToast.Show(owner, "Kaydedildi", durationMs: 0);
                var start = DateTime.UtcNow;
                WaitUntil(() => toast.Opacity >= 1);
                Assert.Equal(1.0, toast.Opacity);
                Assert.True((DateTime.UtcNow - start).TotalMilliseconds < TwToastForm.FadeDuration + 500);

                toast.Dismiss();
                WaitUntil(() => toast.IsDisposed);
                Assert.True(toast.IsDisposed);
            });
        }
        finally
        {
            owner.Close();
        }
    });
}
