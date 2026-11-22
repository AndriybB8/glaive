using StudyPlanner.Domain.Calendar;

namespace StudyPlanner.UnitTests.Calendar;

public class CalendarEventTimeTests
{
    private static readonly DateTime Start = new(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Day = new(2026, 11, 23);

    [Fact]
    public void Timed_SetsInstantsAndNoDays()
    {
        var time = CalendarEventTime.Timed(Start, Start.AddHours(1));

        Assert.False(time.IsAllDay);
        Assert.Equal(Start, time.StartsAt);
        Assert.Equal(Start.AddHours(1), time.EndsAt);
        Assert.Null(time.StartDate);
        Assert.Null(time.EndDate);
    }

    [Fact]
    public void Timed_RejectsNonUtcDates()
    {
        var local = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Local);
        var unspecified = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Unspecified);

        Assert.Throws<ArgumentException>(() => CalendarEventTime.Timed(local, Start.AddHours(2)));
        Assert.Throws<ArgumentException>(() => CalendarEventTime.Timed(Start, unspecified));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Timed_RejectsEndNotAfterStart(int minutes)
    {
        Assert.Throws<ArgumentException>(() => CalendarEventTime.Timed(Start, Start.AddMinutes(minutes)));
    }

    [Fact]
    public void AllDay_SetsDaysAndNoInstants()
    {
        var time = CalendarEventTime.AllDay(Day, Day.AddDays(2));

        Assert.True(time.IsAllDay);
        Assert.Equal(Day, time.StartDate);
        Assert.Equal(Day.AddDays(2), time.EndDate);
        Assert.Null(time.StartsAt);
        Assert.Null(time.EndsAt);
    }

    [Fact]
    public void AllDay_AllowsSingleDay()
    {
        var time = CalendarEventTime.AllDay(Day, Day);

        Assert.Equal(time.StartDate, time.EndDate);
    }

    [Fact]
    public void AllDay_RejectsEndBeforeStart()
    {
        Assert.Throws<ArgumentException>(() => CalendarEventTime.AllDay(Day, Day.AddDays(-1)));
    }

    [Fact]
    public void StartDateIn_UsesLocalDayForTimedEvent()
    {
        var warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

        // 23:30 UTC 22 listopada to już 00:30 23 listopada w Warszawie.
        var time = CalendarEventTime.Timed(new DateTime(2026, 11, 22, 23, 30, 0, DateTimeKind.Utc), Start.AddHours(1));

        Assert.Equal(new DateOnly(2026, 11, 23), time.StartDateIn(warsaw));
    }

    [Fact]
    public void StartDateIn_UsesStartDateForAllDayEvent()
    {
        var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

        Assert.Equal(Day, CalendarEventTime.AllDay(Day, Day).StartDateIn(tokyo));
    }
}
