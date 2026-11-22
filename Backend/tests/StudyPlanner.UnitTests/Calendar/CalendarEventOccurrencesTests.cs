using StudyPlanner.Domain.Calendar;

namespace StudyPlanner.UnitTests.Calendar;

public class CalendarEventOccurrencesTests
{
    private const string Warsaw = "Europe/Warsaw";

    private static readonly TimeZoneInfo WarsawZone = TimeZoneInfo.FindSystemTimeZoneById(Warsaw);
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    private static CalendarEvent Timed(DateTime startsAt, TimeSpan duration, RecurrenceFrequency recurrence, DateOnly? until = null) =>
        CalendarEvent.Create(Guid.NewGuid(), "Wykład", null, CalendarEvent.DefaultColor,
            CalendarEventTime.Timed(startsAt, startsAt + duration), Warsaw, recurrence, until, Now);

    private static CalendarEvent AllDay(DateOnly start, DateOnly end, RecurrenceFrequency recurrence, DateOnly? until = null) =>
        CalendarEvent.Create(Guid.NewGuid(), "Dyżur", null, CalendarEvent.DefaultColor,
            CalendarEventTime.AllDay(start, end), Warsaw, recurrence, until, Now);

    private static CalendarWindow Window(DateOnly from, DateOnly to) => CalendarWindow.For(from, to, WarsawZone);

    [Fact]
    public void Window_UsesLocalMidnightsOfViewer()
    {
        var window = Window(Day(2026, 11, 23), Day(2026, 11, 29));

        Assert.Equal(Utc(2026, 11, 22, 23), window.FromUtc);
        Assert.Equal(Utc(2026, 11, 29, 23), window.ToUtc);
    }

    [Fact]
    public void Daily_ReturnsEveryDayAtSameTime()
    {
        // Poniedziałek 23.11.2026, 10:00 w Warszawie = 09:00 UTC.
        var series = Timed(Utc(2026, 11, 23, 9), TimeSpan.FromHours(1), RecurrenceFrequency.Daily);

        var occurrences = CalendarEventOccurrences.Overlapping(series, Window(Day(2026, 11, 23), Day(2026, 11, 29))).ToList();

        Assert.Equal(7, occurrences.Count);
        Assert.All(occurrences, o => Assert.Equal(9, o.Time.StartsAt!.Value.Hour));
        Assert.Equal(Day(2026, 11, 29), occurrences[^1].Date);
        Assert.Equal(Utc(2026, 11, 29, 10), occurrences[^1].Time.EndsAt);
    }

    [Fact]
    public void Weekly_ReturnsSameWeekdayUntilInclusive()
    {
        var series = Timed(Utc(2026, 11, 23, 9), TimeSpan.FromHours(1), RecurrenceFrequency.Weekly, until: Day(2026, 12, 7));

        var dates = CalendarEventOccurrences.OccurrenceDates(series, Day(2026, 11, 1), Day(2026, 12, 31));

        Assert.Equal([Day(2026, 11, 23), Day(2026, 11, 30), Day(2026, 12, 7)], dates);
    }

    [Fact]
    public void Monthly_SkipsMonthsWithoutThatDay()
    {
        var series = AllDay(Day(2027, 1, 31), Day(2027, 1, 31), RecurrenceFrequency.Monthly);

        var dates = CalendarEventOccurrences.OccurrenceDates(series, Day(2027, 1, 1), Day(2027, 12, 31));

        Assert.Equal(
            [Day(2027, 1, 31), Day(2027, 3, 31), Day(2027, 5, 31), Day(2027, 7, 31), Day(2027, 8, 31), Day(2027, 10, 31), Day(2027, 12, 31)],
            dates);
    }

    [Fact]
    public void Monthly_StartsFromRequestedMonthInLongSeries()
    {
        var series = AllDay(Day(2026, 1, 15), Day(2026, 1, 15), RecurrenceFrequency.Monthly);

        var dates = CalendarEventOccurrences.OccurrenceDates(series, Day(2027, 3, 1), Day(2027, 3, 31));

        Assert.Equal([Day(2027, 3, 15)], dates);
    }

    [Fact]
    public void Daily_StartsFromRequestedDayInLongSeries()
    {
        var series = AllDay(Day(2020, 1, 1), Day(2020, 1, 1), RecurrenceFrequency.Daily);

        var dates = CalendarEventOccurrences.OccurrenceDates(series, Day(2026, 11, 23), Day(2026, 11, 25));

        Assert.Equal([Day(2026, 11, 23), Day(2026, 11, 24), Day(2026, 11, 25)], dates);
    }

    [Fact]
    public void Weekly_KeepsLocalTimeAfterSwitchToWinterTime()
    {
        // 19.10.2026 10:00 czasu letniego (UTC+2); od 25.10 czas zimowy (UTC+1).
        var series = Timed(Utc(2026, 10, 19, 8), TimeSpan.FromMinutes(90), RecurrenceFrequency.Weekly);

        var next = CalendarEventOccurrences.TimeOn(series, Day(2026, 10, 26));

        Assert.Equal(Utc(2026, 10, 26, 9), next.StartsAt);
        Assert.Equal(Utc(2026, 10, 26, 10, 30), next.EndsAt);
    }

    [Fact]
    public void Weekly_KeepsLocalTimeAfterSwitchToSummerTime()
    {
        // 23.03.2026 10:00 czasu zimowego (UTC+1); od 29.03 czas letni (UTC+2).
        var series = Timed(Utc(2026, 3, 23, 9), TimeSpan.FromHours(1), RecurrenceFrequency.Weekly);

        Assert.Equal(Utc(2026, 3, 30, 8), CalendarEventOccurrences.TimeOn(series, Day(2026, 3, 30)).StartsAt);
    }

    [Fact]
    public void SkippedLocalTime_MovesForwardByGap()
    {
        // Codziennie o 02:30; 29.03.2026 godziny 02:00–03:00 nie ma, więc wystąpienie jest o 03:30 (01:30 UTC).
        var series = Timed(Utc(2026, 3, 28, 1, 30), TimeSpan.FromHours(1), RecurrenceFrequency.Daily);

        Assert.Equal(Utc(2026, 3, 29, 1, 30), CalendarEventOccurrences.TimeOn(series, Day(2026, 3, 29)).StartsAt);
        Assert.Equal(Utc(2026, 3, 30, 0, 30), CalendarEventOccurrences.TimeOn(series, Day(2026, 3, 30)).StartsAt);
    }

    [Fact]
    public void RepeatedLocalTime_UsesFirstOccurrence()
    {
        // Codziennie o 02:30; 25.10.2026 ta godzina jest dwa razy – liczy się pierwsza (jeszcze czas letni, 00:30 UTC).
        var series = Timed(Utc(2026, 10, 24, 0, 30), TimeSpan.FromHours(1), RecurrenceFrequency.Daily);

        Assert.Equal(Utc(2026, 10, 25, 0, 30), CalendarEventOccurrences.TimeOn(series, Day(2026, 10, 25)).StartsAt);
        Assert.Equal(Utc(2026, 10, 26, 1, 30), CalendarEventOccurrences.TimeOn(series, Day(2026, 10, 26)).StartsAt);
    }

    [Fact]
    public void MultiDayOccurrence_StartedBeforeWindow_IsIncluded()
    {
        // Co tydzień od poniedziałku do środy; widoczne środa–piątek.
        var series = AllDay(Day(2026, 11, 16), Day(2026, 11, 18), RecurrenceFrequency.Weekly);

        var occurrences = CalendarEventOccurrences.Overlapping(series, Window(Day(2026, 11, 18), Day(2026, 11, 20))).ToList();

        var occurrence = Assert.Single(occurrences);
        Assert.Equal(Day(2026, 11, 16), occurrence.Date);
        Assert.Equal(CalendarEventTime.AllDay(Day(2026, 11, 16), Day(2026, 11, 18)), occurrence.Time);
    }

    [Fact]
    public void OvernightOccurrence_FromPreviousDay_IsIncluded()
    {
        // Codziennie 23:00–01:00 w Warszawie; widoczny jeden dzień – 24.11.
        var series = Timed(Utc(2026, 11, 20, 22), TimeSpan.FromHours(2), RecurrenceFrequency.Daily);

        var dates = CalendarEventOccurrences.Overlapping(series, Window(Day(2026, 11, 24), Day(2026, 11, 24))).Select(o => o.Date);

        Assert.Equal([Day(2026, 11, 23), Day(2026, 11, 24)], dates);
    }

    [Fact]
    public void Overlapping_ReturnsNothingBeforeSeriesStartOrAfterUntil()
    {
        var series = Timed(Utc(2026, 11, 23, 9), TimeSpan.FromHours(1), RecurrenceFrequency.Daily, until: Day(2026, 11, 25));

        Assert.Empty(CalendarEventOccurrences.Overlapping(series, Window(Day(2026, 11, 16), Day(2026, 11, 22))));
        Assert.Empty(CalendarEventOccurrences.Overlapping(series, Window(Day(2026, 11, 26), Day(2026, 11, 30))));
    }

    [Fact]
    public void SingleEvent_IsItsOwnOccurrenceOnlyWhenVisible()
    {
        var single = Timed(Utc(2026, 11, 23, 9), TimeSpan.FromHours(1), RecurrenceFrequency.None);

        var occurrence = Assert.Single(CalendarEventOccurrences.Overlapping(single, Window(Day(2026, 11, 23), Day(2026, 11, 23))));
        Assert.Equal(single.GetTime(), occurrence.Time);
        Assert.Empty(CalendarEventOccurrences.Overlapping(single, Window(Day(2026, 11, 24), Day(2026, 11, 30))));
    }

    [Fact]
    public void IsOccurrence_ChecksRuleStartAndUntil()
    {
        var series = Timed(Utc(2026, 11, 23, 9), TimeSpan.FromHours(1), RecurrenceFrequency.Weekly, until: Day(2026, 12, 14));
        var single = Timed(Utc(2026, 11, 23, 9), TimeSpan.FromHours(1), RecurrenceFrequency.None);

        Assert.True(CalendarEventOccurrences.IsOccurrence(series, Day(2026, 11, 30)));
        Assert.False(CalendarEventOccurrences.IsOccurrence(series, Day(2026, 12, 1)));
        Assert.False(CalendarEventOccurrences.IsOccurrence(series, Day(2026, 11, 16)));
        Assert.False(CalendarEventOccurrences.IsOccurrence(series, Day(2026, 12, 21)));
        Assert.False(CalendarEventOccurrences.IsOccurrence(single, Day(2026, 11, 23)));
    }

    [Fact]
    public void Exception_CannotBeExpanded()
    {
        var series = Timed(Utc(2026, 11, 23, 9), TimeSpan.FromHours(1), RecurrenceFrequency.Weekly);
        var exception = CalendarEvent.CreateCancellation(series, Day(2026, 11, 30), CalendarEventOccurrences.TimeOn(series, Day(2026, 11, 30)), Now);

        Assert.Throws<InvalidOperationException>(() => CalendarEventOccurrences.OccurrenceDates(exception, Day(2026, 11, 1), Day(2026, 12, 31)).ToList());
    }
}
