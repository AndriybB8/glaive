using System.Globalization;
using StudyPlanner.Application.Calendar;
using StudyPlanner.Application.Common;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Domain.Calendar;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.UnitTests.Calendar;

public class CalendarServiceTests
{
    private const string Warsaw = "Europe/Warsaw";

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid FriendId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    // Tydzień 23–29.11.2026 (poniedziałek–niedziela), czas zimowy w Warszawie (UTC+1).
    private static readonly DateOnly Mon23 = new(2026, 11, 23);
    private static readonly DateOnly Sun29 = new(2026, 11, 29);

    private readonly FakeCalendarEventRepository _events = new();
    private readonly FakeCalendarScheduleRepository _schedules = new();
    private readonly TestUser _currentUser = new(OwnerId);
    private readonly CalendarService _service;

    public CalendarServiceTests()
    {
        _service = new CalendarService(_events, _schedules, _currentUser);
    }

    private static DateTime At(DateOnly day, int localHour, int minute = 0) =>
        day.ToDateTime(new TimeOnly(localHour, minute), DateTimeKind.Utc).AddHours(-1);

    private CalendarEvent AddEvent(
        DateOnly day,
        int localHour = 10,
        RecurrenceFrequency recurrence = RecurrenceFrequency.None,
        Guid? userId = null,
        string title = "Wykład")
    {
        var calendarEvent = CalendarEvent.Create(userId ?? OwnerId, title, null, "#16A34A",
            CalendarEventTime.Timed(At(day, localHour), At(day, localHour).AddHours(1)), Warsaw, recurrence, null, Now);
        _events.Events.Add(calendarEvent);
        return calendarEvent;
    }

    private async Task<IReadOnlyList<CalendarItemDto>> GetWeekAsync(DateOnly? from = null, DateOnly? to = null)
    {
        var result = await _service.GetAsync(from ?? Mon23, to ?? Sun29, Warsaw, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    [Theory]
    [InlineData(null, "2026-11-29", Warsaw)]
    [InlineData("2026-11-23", null, Warsaw)]
    [InlineData("2026-11-29", "2026-11-23", Warsaw)]
    [InlineData("2026-11-01", "2027-01-02", Warsaw)] // 63 dni
    [InlineData("2026-11-23", "2026-11-29", null)]
    [InlineData("2026-11-23", "2026-11-29", "Mars/Olympus")]
    [InlineData("2026-11-23", "2026-11-29", "Central European Standard Time")]
    public async Task Get_RejectsInvalidRangeOrTimeZone(string? from, string? to, string? timeZone)
    {
        var result = await _service.GetAsync(
            from is null ? null : DateOnly.Parse(from, CultureInfo.InvariantCulture),
            to is null ? null : DateOnly.Parse(to, CultureInfo.InvariantCulture),
            timeZone,
            CancellationToken.None);

        Assert.Equal(CalendarErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Get_AllowsMaxRange()
    {
        var result = await _service.GetAsync(Mon23, Mon23.AddDays(CalendarService.MaxRangeDays - 1), Warsaw, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Get_MergesEventsSchedulesAndTasksWithRoles()
    {
        var lecture = AddEvent(Mon23.AddDays(1));
        var own = Schedule.Create(OwnerId, "Sesja zimowa", new DateOnly(2026, 11, 1), new DateOnly(2027, 2, 14), Now);
        var shared = Schedule.Create(FriendId, "Projekt zespołowy", Mon23, Mon23.AddDays(2), Now);
        var task = TaskItem.Create(FriendId, shared.Id, "Raport", "Rozdział 2", Mon23.AddDays(2), TaskPriority.High, TaskItemStatus.InProgress, Now);
        _schedules.Schedules.Add(new ScheduleWithAccess(own, AccessRole.Owner));
        _schedules.Schedules.Add(new ScheduleWithAccess(shared, AccessRole.Viewer));
        _schedules.Tasks.Add(new CalendarTask(task, shared.Name, AccessRole.Viewer));

        var items = await GetWeekAsync();

        Assert.Equal(4, items.Count);

        var ownItem = Assert.Single(items, i => i.Id == own.Id);
        Assert.Equal(CalendarItemKind.Schedule, ownItem.Kind);
        Assert.Equal(AccessRole.Owner, ownItem.Access);
        Assert.True(ownItem.IsAllDay);
        Assert.Equal(own.StartDate, ownItem.StartDate);
        Assert.Equal(own.EndDate, ownItem.EndDate);

        var sharedItem = Assert.Single(items, i => i.Id == shared.Id);
        Assert.Equal(AccessRole.Viewer, sharedItem.Access);
        Assert.Equal($"schedule:{shared.Id}", sharedItem.Key);

        var taskItem = Assert.Single(items, i => i.Kind == CalendarItemKind.Task);
        Assert.Equal(task.Id, taskItem.Id);
        Assert.Equal("Raport", taskItem.Title);
        Assert.Equal("Rozdział 2", taskItem.Description);
        Assert.Equal(task.DueDate, taskItem.StartDate);
        Assert.Equal(task.DueDate, taskItem.EndDate);
        Assert.Equal(AccessRole.Viewer, taskItem.Access);
        Assert.Equal(shared.Id, taskItem.ScheduleId);
        Assert.Equal("Projekt zespołowy", taskItem.ScheduleName);
        Assert.Equal(TaskItemStatus.InProgress, taskItem.TaskStatus);
        Assert.Equal(TaskPriority.High, taskItem.TaskPriority);

        var eventItem = Assert.Single(items, i => i.Kind == CalendarItemKind.Event);
        Assert.Equal(lecture.Id, eventItem.Id);
        Assert.Equal($"event:{lecture.Id}", eventItem.Key);
        Assert.Equal(AccessRole.Owner, eventItem.Access);
        Assert.Equal("#16A34A", eventItem.Color);
        Assert.False(eventItem.IsAllDay);
        Assert.False(eventItem.IsRecurring);
        Assert.Null(eventItem.OccurrenceDate);
        Assert.Equal(1, eventItem.Version);
        Assert.Null(eventItem.SeriesVersion);
    }

    [Fact]
    public async Task Get_SortsByDayThenAllDayThenTime()
    {
        var late = AddEvent(Mon23, 15, title: "Później");
        var early = AddEvent(Mon23, 9, title: "Wcześniej");
        var nextDay = AddEvent(Mon23.AddDays(1), 8, title: "Jutro");
        var schedule = Schedule.Create(OwnerId, "Sesja", Mon23, Sun29, Now);
        _schedules.Schedules.Add(new ScheduleWithAccess(schedule, AccessRole.Owner));

        var items = await GetWeekAsync();

        Assert.Equal([schedule.Id, early.Id, late.Id, nextDay.Id], items.Select(i => i.Id));
    }

    [Fact]
    public async Task Get_ShowsOnlyCurrentUsersEvents()
    {
        AddEvent(Mon23, userId: FriendId);

        Assert.Empty(await GetWeekAsync());
    }

    [Fact]
    public async Task Get_UsesViewerDayBoundaries()
    {
        var lastEvening = AddEvent(Sun29, 23);                   // 29.11 23:00 w Warszawie – jeszcze widoczne
        AddEvent(Sun29.AddDays(1), 0);                           // 30.11 00:00 – już następny tydzień
        AddEvent(Mon23.AddDays(-1), 22);                         // 22.11 22:00–23:00 – kończy się przed północą

        var items = await GetWeekAsync();

        Assert.Equal(lastEvening.Id, Assert.Single(items).Id);
    }

    [Fact]
    public async Task Get_ExpandsSeriesIntoOccurrences()
    {
        var series = AddEvent(new DateOnly(2026, 11, 2), recurrence: RecurrenceFrequency.Daily);

        var items = await GetWeekAsync();

        Assert.Equal(7, items.Count);
        Assert.All(items, i =>
        {
            Assert.Equal(series.Id, i.Id);
            Assert.True(i.IsRecurring);
            Assert.False(i.IsException);
            Assert.Equal(CalendarEventService.UnchangedOccurrenceVersion, i.Version);
            Assert.Equal(1, i.SeriesVersion);
        });
        Assert.Equal(Mon23, items[0].OccurrenceDate);
        Assert.Equal($"event:{series.Id}:2026-11-23", items[0].Key);
        Assert.Equal(At(Mon23, 10), items[0].StartsAt);
    }

    [Fact]
    public async Task Get_SkipsCancelledOccurrenceAndShowsChangedOneAtNewTime()
    {
        var series = AddEvent(new DateOnly(2026, 11, 2), recurrence: RecurrenceFrequency.Daily);
        var tuesday = Mon23.AddDays(1);
        var wednesday = Mon23.AddDays(2);
        _events.Events.Add(CalendarEvent.CreateCancellation(series, tuesday, CalendarEventOccurrences.TimeOn(series, tuesday), Now));
        var changed = CalendarEvent.CreateException(series, wednesday, "Wykład – sala 10", null, "#DC2626",
            CalendarEventTime.Timed(At(wednesday, 14), At(wednesday, 16)), Now);
        changed.UpdateOccurrence("Wykład – sala 10", null, "#DC2626", CalendarEventTime.Timed(At(wednesday, 14), At(wednesday, 16)), Now);
        _events.Events.Add(changed);

        var items = await GetWeekAsync();

        Assert.Equal(6, items.Count);
        Assert.DoesNotContain(items, i => i.OccurrenceDate == tuesday);

        var changedItem = Assert.Single(items, i => i.OccurrenceDate == wednesday);
        Assert.Equal(series.Id, changedItem.Id);
        Assert.True(changedItem.IsException);
        Assert.Equal("Wykład – sala 10", changedItem.Title);
        Assert.Equal("#DC2626", changedItem.Color);
        Assert.Equal(At(wednesday, 14), changedItem.StartsAt);
        Assert.Equal(2, changedItem.Version);
        Assert.Equal(1, changedItem.SeriesVersion);
        Assert.Equal($"event:{series.Id}:2026-11-25", changedItem.Key);
    }

    [Fact]
    public async Task Get_OccurrenceMovedIntoWindow_IsShownAndMovedOutIsHidden()
    {
        var series = AddEvent(new DateOnly(2026, 11, 16), recurrence: RecurrenceFrequency.Weekly);
        var nextMonday = Sun29.AddDays(1);

        // Wystąpienie z 30.11 przeniesione na 27.11 (do widocznego tygodnia), a to z 23.11 – na 16.12 (poza tydzień).
        _events.Events.Add(CalendarEvent.CreateException(series, nextMonday, "Wykład", null, "#16A34A",
            CalendarEventTime.Timed(At(Mon23.AddDays(4), 10), At(Mon23.AddDays(4), 11)), Now));
        _events.Events.Add(CalendarEvent.CreateException(series, Mon23, "Wykład", null, "#16A34A",
            CalendarEventTime.Timed(At(new DateOnly(2026, 12, 16), 10), At(new DateOnly(2026, 12, 16), 11)), Now));

        var item = Assert.Single(await GetWeekAsync());

        Assert.Equal(nextMonday, item.OccurrenceDate);
        Assert.Equal(At(Mon23.AddDays(4), 10), item.StartsAt);
    }

    [Fact]
    public async Task Get_IgnoresExceptionOfDayThatIsNoLongerOccurrence()
    {
        var series = AddEvent(new DateOnly(2026, 11, 16), recurrence: RecurrenceFrequency.Weekly);
        var exception = CalendarEvent.CreateException(series, Mon23, "Wykład", null, "#16A34A",
            CalendarEventTime.Timed(At(Mon23, 12), At(Mon23, 13)), Now);
        _events.Events.Add(exception);

        // Seria zmieniona na wtorki – poniedziałkowy wyjątek nie ma już czego zastępować.
        series.Update("Wykład", null, "#16A34A",
            CalendarEventTime.Timed(At(new DateOnly(2026, 11, 17), 10), At(new DateOnly(2026, 11, 17), 11)),
            Warsaw, RecurrenceFrequency.Weekly, null, Now);

        var item = Assert.Single(await GetWeekAsync());

        Assert.Equal(Mon23.AddDays(1), item.OccurrenceDate);
        Assert.False(item.IsException);
    }

    private sealed class TestUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; set; } = userId;
    }

    /// <summary>Harmonogramy i zadania już z dostępem – filtruje tylko po datach, jak repozytorium.</summary>
    private sealed class FakeCalendarScheduleRepository : ICalendarScheduleRepository
    {
        public List<ScheduleWithAccess> Schedules { get; } = [];

        public List<CalendarTask> Tasks { get; } = [];

        public Task<IReadOnlyList<ScheduleWithAccess>> GetSchedulesAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ScheduleWithAccess>>(
                Schedules.Where(s => s.Schedule.StartDate <= to && s.Schedule.EndDate >= from).ToList());

        public Task<IReadOnlyList<CalendarTask>> GetTasksDueAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CalendarTask>>(
                Tasks.Where(t => t.Task.DueDate >= from && t.Task.DueDate <= to).ToList());
    }
}
