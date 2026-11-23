using StudyPlanner.Domain.Calendar;

namespace StudyPlanner.UnitTests.Calendar;

public class CalendarEventTests
{
    private const string Warsaw = "Europe/Warsaw";
    private const string Blue = "#2563EB";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Poniedziałek 23.11.2026, 10:00–11:30 w Warszawie (UTC+1).</summary>
    private static readonly CalendarEventTime Lecture =
        CalendarEventTime.Timed(new DateTime(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 23, 10, 30, 0, DateTimeKind.Utc));

    private static readonly DateOnly FirstDay = new(2026, 11, 23);

    private static CalendarEvent CreateEvent(
        RecurrenceFrequency recurrence = RecurrenceFrequency.None,
        DateOnly? until = null,
        CalendarEventTime? time = null) =>
        CalendarEvent.Create(UserId, "Wykład", "Sala 204", Blue, time ?? Lecture, Warsaw, recurrence, until, Now);

    private static CalendarEvent CreateSeries(DateOnly? until = null) => CreateEvent(RecurrenceFrequency.Weekly, until);

    private static CalendarEventTime MovedTime(int days, int hours = 0) =>
        CalendarEventTime.Timed(Lecture.StartsAt!.Value.AddDays(days).AddHours(hours), Lecture.EndsAt!.Value.AddDays(days).AddHours(hours));

    [Fact]
    public void Create_Timed_SetsFieldsAndFirstVersion()
    {
        var calendarEvent = CreateEvent();

        Assert.NotEqual(Guid.Empty, calendarEvent.Id);
        Assert.Equal(UserId, calendarEvent.UserId);
        Assert.Equal("Wykład", calendarEvent.Title);
        Assert.Equal("Sala 204", calendarEvent.Description);
        Assert.Equal(Blue, calendarEvent.Color);
        Assert.False(calendarEvent.IsAllDay);
        Assert.Equal(Lecture.StartsAt, calendarEvent.StartsAt);
        Assert.Equal(Lecture.EndsAt, calendarEvent.EndsAt);
        Assert.Null(calendarEvent.StartDate);
        Assert.Null(calendarEvent.EndDate);
        Assert.Equal(Warsaw, calendarEvent.TimeZone);
        Assert.Equal(RecurrenceFrequency.None, calendarEvent.Recurrence);
        Assert.Null(calendarEvent.RecurrenceUntil);
        Assert.Null(calendarEvent.SeriesId);
        Assert.Null(calendarEvent.OriginalDate);
        Assert.False(calendarEvent.IsCancelled);
        Assert.Equal(Now, calendarEvent.CreatedAt);
        Assert.Equal(Now, calendarEvent.UpdatedAt);
        Assert.Equal(1, calendarEvent.Version);
        Assert.False(calendarEvent.IsRecurring());
        Assert.False(calendarEvent.IsException());
    }

    [Fact]
    public void Create_AllDay_SetsDaysOnly()
    {
        var calendarEvent = CreateEvent(time: CalendarEventTime.AllDay(FirstDay, FirstDay.AddDays(1)));

        Assert.True(calendarEvent.IsAllDay);
        Assert.Equal(FirstDay, calendarEvent.StartDate);
        Assert.Equal(FirstDay.AddDays(1), calendarEvent.EndDate);
        Assert.Null(calendarEvent.StartsAt);
        Assert.Null(calendarEvent.EndsAt);
        Assert.Equal(CalendarEventTime.AllDay(FirstDay, FirstDay.AddDays(1)), calendarEvent.GetTime());
    }

    [Fact]
    public void Create_WithoutDescription_KeepsNull()
    {
        var calendarEvent = CalendarEvent.Create(UserId, "Wykład", null, Blue, Lecture, Warsaw, RecurrenceFrequency.None, null, Now);

        Assert.Null(calendarEvent.Description);
    }

    [Fact]
    public void Create_Series_SetsRecurrence()
    {
        var calendarEvent = CreateSeries(until: new DateOnly(2027, 1, 25));

        Assert.True(calendarEvent.IsRecurring());
        Assert.Equal(RecurrenceFrequency.Weekly, calendarEvent.Recurrence);
        Assert.Equal(new DateOnly(2027, 1, 25), calendarEvent.RecurrenceUntil);
        Assert.Equal(FirstDay, calendarEvent.LocalStartDate());
    }

    [Fact]
    public void Create_RejectsNonUtcDate()
    {
        var local = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() =>
            CalendarEvent.Create(UserId, "Wykład", null, Blue, Lecture, Warsaw, RecurrenceFrequency.None, null, local));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankTitle(string title)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            CalendarEvent.Create(UserId, title, null, Blue, Lecture, Warsaw, RecurrenceFrequency.None, null, Now));
    }

    [Fact]
    public void Create_RejectsTooLongTitle()
    {
        var title = new string('a', CalendarEvent.TitleMaxLength + 1);

        Assert.Throws<ArgumentException>(() =>
            CalendarEvent.Create(UserId, title, null, Blue, Lecture, Warsaw, RecurrenceFrequency.None, null, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(CalendarEvent.DescriptionMaxLength + 1)]
    public void Create_RejectsEmptyOrTooLongDescription(int length)
    {
        var description = new string('a', length);

        Assert.Throws<ArgumentException>(() =>
            CalendarEvent.Create(UserId, "Wykład", description, Blue, Lecture, Warsaw, RecurrenceFrequency.None, null, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("blue")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGGGGG")]
    public void Create_RejectsInvalidColor(string color)
    {
        Assert.Throws<ArgumentException>(() =>
            CalendarEvent.Create(UserId, "Wykład", null, color, Lecture, Warsaw, RecurrenceFrequency.None, null, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Mars/Olympus")]
    [InlineData("Central European Standard Time")]
    public void Create_RejectsUnknownOrNonIanaTimeZone(string timeZone)
    {
        Assert.Throws<ArgumentException>(() =>
            CalendarEvent.Create(UserId, "Wykład", null, Blue, Lecture, timeZone, RecurrenceFrequency.None, null, Now));
    }

    [Fact]
    public void Create_StoresCanonicalTimeZoneId()
    {
        var calendarEvent = CalendarEvent.Create(UserId, "Wykład", null, Blue, Lecture, "europe/warsaw", RecurrenceFrequency.None, null, Now);

        Assert.Equal(Warsaw, calendarEvent.TimeZone);
    }

    [Fact]
    public void Create_RejectsUnknownRecurrence()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvent((RecurrenceFrequency)99));
    }

    [Fact]
    public void Create_RejectsUntilWithoutRecurrence()
    {
        Assert.Throws<ArgumentException>(() => CreateEvent(RecurrenceFrequency.None, FirstDay.AddDays(7)));
    }

    [Fact]
    public void Create_UntilIsComparedWithLocalFirstDay()
    {
        // 23:30 UTC 22 listopada = 00:30 23 listopada w Warszawie, więc pierwsze wystąpienie jest 23 listopada.
        var lateEvening = CalendarEventTime.Timed(
            new DateTime(2026, 11, 22, 23, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 23, 0, 30, 0, DateTimeKind.Utc));

        Assert.Throws<ArgumentException>(() => CreateEvent(RecurrenceFrequency.Daily, new DateOnly(2026, 11, 22), lateEvening));

        var calendarEvent = CreateEvent(RecurrenceFrequency.Daily, new DateOnly(2026, 11, 23), lateEvening);
        Assert.Equal(new DateOnly(2026, 11, 23), calendarEvent.RecurrenceUntil);
    }

    [Fact]
    public void Update_ChangesEverythingAndBumpsVersion()
    {
        var calendarEvent = CreateEvent();
        var later = Now.AddHours(1);
        var allDay = CalendarEventTime.AllDay(FirstDay, FirstDay);

        calendarEvent.Update("Egzamin", null, "#DC2626", allDay, "UTC", RecurrenceFrequency.Monthly, FirstDay.AddMonths(3), later);

        Assert.Equal("Egzamin", calendarEvent.Title);
        Assert.Null(calendarEvent.Description);
        Assert.Equal("#DC2626", calendarEvent.Color);
        Assert.Equal(allDay, calendarEvent.GetTime());
        Assert.Null(calendarEvent.StartsAt);
        Assert.Equal("UTC", calendarEvent.TimeZone);
        Assert.Equal(RecurrenceFrequency.Monthly, calendarEvent.Recurrence);
        Assert.Equal(FirstDay.AddMonths(3), calendarEvent.RecurrenceUntil);
        Assert.Equal(later, calendarEvent.UpdatedAt);
        Assert.Equal(Now, calendarEvent.CreatedAt);
        Assert.Equal(2, calendarEvent.Version);
    }

    [Fact]
    public void Update_RejectsInvalidValuesWithoutChanges()
    {
        var calendarEvent = CreateEvent();

        Assert.Throws<ArgumentException>(() =>
            calendarEvent.Update("Wykład", null, "red", Lecture, Warsaw, RecurrenceFrequency.None, null, Now.AddHours(1)));

        Assert.Equal(Blue, calendarEvent.Color);
        Assert.Equal(1, calendarEvent.Version);
    }

    [Fact]
    public void Update_RejectsException()
    {
        var series = CreateSeries();
        var exception = CalendarEvent.CreateException(series, FirstDay.AddDays(7), "Wykład", null, Blue, MovedTime(7), Now);

        Assert.Throws<InvalidOperationException>(() =>
            exception.Update("Wykład", null, Blue, Lecture, Warsaw, RecurrenceFrequency.None, null, Now));
    }

    [Fact]
    public void CreateException_LinksOccurrenceAndCopiesOwnerAndTimeZone()
    {
        var series = CreateSeries();
        var originalDate = FirstDay.AddDays(7);
        var moved = MovedTime(8, hours: 2);

        var exception = CalendarEvent.CreateException(series, originalDate, "Wykład (sala 10)", null, "#16A34A", moved, Now.AddHours(1));

        Assert.NotEqual(series.Id, exception.Id);
        Assert.Equal(series.Id, exception.SeriesId);
        Assert.Equal(originalDate, exception.OriginalDate);
        Assert.Equal(series.UserId, exception.UserId);
        Assert.Equal(series.TimeZone, exception.TimeZone);
        Assert.Equal("Wykład (sala 10)", exception.Title);
        Assert.Equal("#16A34A", exception.Color);
        Assert.Equal(moved, exception.GetTime());
        Assert.Equal(RecurrenceFrequency.None, exception.Recurrence);
        Assert.Null(exception.RecurrenceUntil);
        Assert.False(exception.IsCancelled);
        Assert.True(exception.IsException());
        Assert.Equal(1, exception.Version);
    }

    [Fact]
    public void CreateException_RejectsEventThatIsNotSeries()
    {
        var single = CreateEvent();

        Assert.Throws<InvalidOperationException>(() =>
            CalendarEvent.CreateException(single, FirstDay, "Wykład", null, Blue, Lecture, Now));
    }

    [Fact]
    public void CreateException_RejectsExceptionAsSeries()
    {
        var series = CreateSeries();
        var exception = CalendarEvent.CreateException(series, FirstDay.AddDays(7), "Wykład", null, Blue, MovedTime(7), Now);

        Assert.Throws<InvalidOperationException>(() =>
            CalendarEvent.CreateException(exception, FirstDay.AddDays(7), "Wykład", null, Blue, MovedTime(7), Now));
    }

    [Fact]
    public void CreateException_RejectsDateOutsideSeries()
    {
        var series = CreateSeries(until: FirstDay.AddDays(14));

        Assert.Throws<ArgumentException>(() =>
            CalendarEvent.CreateException(series, FirstDay.AddDays(-7), "Wykład", null, Blue, MovedTime(-7), Now));
        Assert.Throws<ArgumentException>(() =>
            CalendarEvent.CreateException(series, FirstDay.AddDays(21), "Wykład", null, Blue, MovedTime(21), Now));
    }

    [Fact]
    public void CreateCancellation_IsCancelledAndKeepsSeriesDetails()
    {
        var series = CreateSeries();

        var cancellation = CalendarEvent.CreateCancellation(series, FirstDay.AddDays(7), MovedTime(7), Now);

        Assert.True(cancellation.IsCancelled);
        Assert.Equal(series.Id, cancellation.SeriesId);
        Assert.Equal(FirstDay.AddDays(7), cancellation.OriginalDate);
        Assert.Equal(series.Title, cancellation.Title);
        Assert.Equal(series.Description, cancellation.Description);
        Assert.Equal(series.Color, cancellation.Color);
        Assert.Equal(MovedTime(7), cancellation.GetTime());
    }

    [Fact]
    public void UpdateOccurrence_ChangesExceptionAndBumpsVersion()
    {
        var series = CreateSeries();
        var exception = CalendarEvent.CreateException(series, FirstDay.AddDays(7), "Wykład", null, Blue, MovedTime(7), Now);
        var later = Now.AddHours(1);

        exception.UpdateOccurrence("Wykład odwołany?", "Sprawdzić", "#DC2626", MovedTime(9), later);

        Assert.Equal("Wykład odwołany?", exception.Title);
        Assert.Equal("Sprawdzić", exception.Description);
        Assert.Equal("#DC2626", exception.Color);
        Assert.Equal(MovedTime(9), exception.GetTime());
        Assert.Equal(FirstDay.AddDays(7), exception.OriginalDate);
        Assert.Equal(later, exception.UpdatedAt);
        Assert.Equal(2, exception.Version);
    }

    [Fact]
    public void UpdateOccurrence_RejectsSeriesAndCancelledOccurrence()
    {
        var series = CreateSeries();
        var cancellation = CalendarEvent.CreateCancellation(series, FirstDay.AddDays(7), MovedTime(7), Now);

        Assert.Throws<InvalidOperationException>(() => series.UpdateOccurrence("Wykład", null, Blue, Lecture, Now));
        Assert.Throws<InvalidOperationException>(() => cancellation.UpdateOccurrence("Wykład", null, Blue, MovedTime(7), Now));
    }

    [Fact]
    public void Cancel_MarksExceptionOnlyOnce()
    {
        var series = CreateSeries();
        var exception = CalendarEvent.CreateException(series, FirstDay.AddDays(7), "Wykład", null, Blue, MovedTime(7), Now);
        var later = Now.AddHours(1);

        exception.Cancel(later);
        exception.Cancel(later.AddHours(1));

        Assert.True(exception.IsCancelled);
        Assert.Equal(later, exception.UpdatedAt);
        Assert.Equal(2, exception.Version);
    }

    [Fact]
    public void Cancel_RejectsEventThatIsNotException()
    {
        Assert.Throws<InvalidOperationException>(() => CreateEvent().Cancel(Now));
        Assert.Throws<InvalidOperationException>(() => CreateSeries().Cancel(Now));
    }

    [Fact]
    public void EndRecurrenceBefore_SetsUntilToPreviousDay()
    {
        var series = CreateSeries();
        var later = Now.AddHours(1);

        series.EndRecurrenceBefore(FirstDay.AddDays(14), later);

        Assert.Equal(FirstDay.AddDays(13), series.RecurrenceUntil);
        Assert.Equal(later, series.UpdatedAt);
        Assert.Equal(2, series.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public void EndRecurrenceBefore_RejectsFirstOccurrenceOrEarlier(int days)
    {
        var series = CreateSeries();

        Assert.Throws<ArgumentException>(() => series.EndRecurrenceBefore(FirstDay.AddDays(days), Now));
        Assert.Null(series.RecurrenceUntil);
    }

    [Fact]
    public void EndRecurrenceBefore_RejectsDateAfterSeriesEnd()
    {
        var series = CreateSeries(until: FirstDay.AddDays(14));

        Assert.Throws<ArgumentException>(() => series.EndRecurrenceBefore(FirstDay.AddDays(21), Now));
        Assert.Equal(FirstDay.AddDays(14), series.RecurrenceUntil);
    }

    [Fact]
    public void EndRecurrenceBefore_RejectsEventThatIsNotSeries()
    {
        Assert.Throws<InvalidOperationException>(() => CreateEvent().EndRecurrenceBefore(FirstDay.AddDays(7), Now));
    }

    [Theory]
    [InlineData("Europe/Warsaw", true)]
    [InlineData("UTC", true)]
    [InlineData("Central European Standard Time", false)]
    [InlineData("Mars/Olympus", false)]
    [InlineData(null, false)]
    public void IsValidTimeZone_AcceptsOnlyIanaIds(string? timeZone, bool expected)
    {
        Assert.Equal(expected, CalendarEvent.IsValidTimeZone(timeZone));
    }
}
