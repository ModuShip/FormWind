using System;
using System.Linq;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Shift hours of <see cref="TwPeriodPicker"/> (0.1.8 report: the application kept its own shift picker): the time windows
/// of a period for one shift, night shifts past midnight and selecting the shift running now.
/// </summary>
[Collection(UiCollection.Name)]
public class PeriodShiftHoursTests
{
    private static TwPeriodPicker Picker()
    {
        var picker = new TwPeriodPicker();
        foreach (var shift in new[] { "A", "B", "C" }) picker.Shifts.Add(shift);
        picker.SetShiftHours("A", TimeSpan.FromHours(6), TimeSpan.FromHours(14));
        picker.SetShiftHours("B", TimeSpan.FromHours(14), TimeSpan.FromHours(22));
        picker.SetShiftHours("C", TimeSpan.FromHours(22), TimeSpan.FromHours(6));
        picker.SetRange(new DateTime(2026, 3, 1), new DateTime(2026, 3, 3));
        return picker;
    }

    [Fact]
    public void Without_a_shift_the_period_is_one_window_from_midnight_to_midnight() => Sta.Run(() =>
    {
        using var picker = Picker();
        var window = Assert.Single(picker.GetShiftWindows());
        Assert.Equal((new DateTime(2026, 3, 1), new DateTime(2026, 3, 4)), window);
    });

    [Fact]
    public void A_shift_gives_one_window_per_day_and_a_night_shift_runs_past_midnight() => Sta.Run(() =>
    {
        using var picker = Picker();
        picker.Shift = "B";
        Assert.Equal(new[]
        {
            (new DateTime(2026, 3, 1, 14, 0, 0), new DateTime(2026, 3, 1, 22, 0, 0)),
            (new DateTime(2026, 3, 2, 14, 0, 0), new DateTime(2026, 3, 2, 22, 0, 0)),
            (new DateTime(2026, 3, 3, 14, 0, 0), new DateTime(2026, 3, 3, 22, 0, 0)),
        }, picker.GetShiftWindows().ToArray());

        picker.Shift = "C";
        var night = picker.GetShiftWindows();
        Assert.Equal(3, night.Count);
        Assert.Equal((new DateTime(2026, 3, 3, 22, 0, 0), new DateTime(2026, 3, 4, 6, 0, 0)), night[2]);
    });

    [Fact]
    public void A_shift_without_hours_covers_the_whole_days() => Sta.Run(() =>
    {
        using var picker = Picker();
        picker.Shifts.Add("D");
        picker.Shift = "D";
        Assert.Null(picker.GetShiftHours("D"));
        Assert.Single(picker.GetShiftWindows());
    });

    [Theory]
    [InlineData(7, "A")]
    [InlineData(14, "B")]
    [InlineData(23, "C")]
    [InlineData(3, "C")]
    public void The_shift_running_now_is_selected(int hour, string expected) => Sta.Run(() =>
    {
        using var picker = Picker();
        Assert.Equal(expected, picker.SelectCurrentShift(new DateTime(2026, 3, 2, hour, 30, 0)));
        Assert.Equal(expected, picker.Shift);
    });

    [Fact]
    public void No_shift_is_selected_when_none_runs_now_and_bad_hours_are_refused() => Sta.Run(() =>
    {
        using var picker = new TwPeriodPicker();
        picker.Shifts.Add("A");
        picker.SetShiftHours("A", TimeSpan.FromHours(6), TimeSpan.FromHours(14));
        picker.Shift = "A";
        Assert.Null(picker.SelectCurrentShift(new DateTime(2026, 3, 2, 20, 0, 0)));
        Assert.Equal("A", picker.Shift);
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SetShiftHours("A", TimeSpan.FromHours(24), TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SetShiftHours("A", TimeSpan.Zero, TimeSpan.FromHours(-1)));
        Assert.Throws<ArgumentNullException>(() => picker.SetShiftHours(null!, TimeSpan.Zero, TimeSpan.Zero));
    });
}
