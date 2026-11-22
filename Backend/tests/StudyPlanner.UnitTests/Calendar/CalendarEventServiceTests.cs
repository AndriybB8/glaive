using StudyPlanner.Application.Calendar;
using StudyPlanner.Application.Common;
using StudyPlanner.Domain.Calendar;

namespace StudyPlanner.UnitTests.Calendar;

public class CalendarEventServiceTests
{
    private const string Warsaw = "Europe/Warsaw";

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    // Poniedziałki (czas zimowy w Warszawie, UTC+1).
    private static readonly DateOnly Mon23 = new(2026, 11, 23);
    private static readonly DateOnly Mon30 = new(2026, 11, 30);
    private static readonly DateOnly Mon7 = new(2026, 12, 7);
    private static readonly DateOnly Mon14 = new(2026, 12, 14);
    private static readonly DateOnly Jan25 = new(2027, 1, 25);

    private readonly FakeCalendarEventRepository _events = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero));
    private readonly TestUser _currentUser = new(OwnerId);
    private readonly CalendarEventService _service;

    public CalendarEventServiceTests()
    {
        _service = new CalendarEventService(_events, _currentUser, _time);
    }

    /// <summary>Pełna godzina czasu lokalnego w Warszawie (zimą UTC+1) jako UTC.</summary>
    private static DateTime At(DateOnly day, int localHour) => day.ToDateTime(new TimeOnly(localHour - 1, 0), DateTimeKind.Utc);

    private static CreateCalendarEventRequest Timed(
        DateOnly day,
        int localHour = 10,
        RecurrenceFrequency? recurrence = null,
        DateOnly? until = null) =>
        new("Wykład", null, null, false, At(day, localHour), At(day, localHour).AddHours(1), null, null, Warsaw, recurrence, until);

    private static UpdateCalendarEventRequest Edit(
        DateOnly day,
        int localHour,
        int version,
        CalendarEditScope? scope = null,
        DateOnly? occurrenceDate = null,
        string title = "Wykład",
        int hours = 1,
        RecurrenceFrequency? recurrence = null,
        DateOnly? until = null) =>
        new(title, null, null, false, At(day, localHour), At(day, localHour).AddHours(hours), null, null, Warsaw, recurrence, until, version, scope, occurrenceDate);

    /// <summary>Edycja wystąpienia serii co tydzień (bez zmiany powtarzania w "following"/"all").</summary>
    private static UpdateCalendarEventRequest EditSeries(
        DateOnly day,
        int localHour,
        int version,
        CalendarEditScope scope,
        DateOnly? occurrenceDate,
        string title = "Wykład",
        int hours = 1,
        DateOnly? until = null) =>
        Edit(day, localHour, version, scope, occurrenceDate, title, hours,
            scope == CalendarEditScope.Single ? null : RecurrenceFrequency.Weekly, until);

    private async Task<CalendarEventDto> CreateAsync(CreateCalendarEventRequest request) =>
        (await _service.CreateAsync(request, CancellationToken.None)).Value!;

    private Task<CalendarEventDto> CreateWeeklyAsync(DateOnly? until = null) =>
        CreateAsync(Timed(Mon23, recurrence: RecurrenceFrequency.Weekly, until: until));

    private Task<CalendarResult<CalendarEventDto>> UpdateAsync(Guid id, UpdateCalendarEventRequest request) =>
        _service.UpdateAsync(id, request, CancellationToken.None);

    private Task<CalendarResult<bool>> DeleteAsync(Guid id, CalendarEditScope? scope = null, DateOnly? occurrenceDate = null) =>
        _service.DeleteAsync(id, scope, occurrenceDate, CancellationToken.None);

    private CalendarEvent Stored(Guid id) => _events.Events.Single(e => e.Id == id);

    private CalendarEvent? ExceptionOn(Guid seriesId, DateOnly date) =>
        _events.Events.SingleOrDefault(e => e.SeriesId == seriesId && e.OriginalDate == date);

    // ---------- Tworzenie i odczyt ----------

    [Fact]
    public async Task Create_TrimsValuesAndUsesDefaults()
    {
        var result = await _service.CreateAsync(
            new("  Wykład z analizy  ", "   ", null, null, At(Mon23, 10), At(Mon23, 11), null, null, " Europe/Warsaw ", null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var created = result.Value!;
        Assert.Equal("Wykład z analizy", created.Title);
        Assert.Null(created.Description);
        Assert.Equal(CalendarEvent.DefaultColor, created.Color);
        Assert.False(created.IsAllDay);
        Assert.Equal(At(Mon23, 10), created.StartsAt);
        Assert.Equal(At(Mon23, 11), created.EndsAt);
        Assert.Equal(Warsaw, created.TimeZone);
        Assert.Equal(RecurrenceFrequency.None, created.Recurrence);
        Assert.Equal(1, created.Version);
        Assert.Equal(OwnerId, Assert.Single(_events.Events).UserId);
    }

    [Fact]
    public async Task Create_AllDayWithoutEndDate_LastsOneDay()
    {
        var created = await CreateAsync(new("Egzamin", "Sala 204", "#DC2626", true, null, null, Mon23, null, Warsaw, null, null));

        Assert.True(created.IsAllDay);
        Assert.Equal(Mon23, created.StartDate);
        Assert.Equal(Mon23, created.EndDate);
        Assert.Null(created.StartsAt);
        Assert.Equal("#DC2626", created.Color);
        Assert.Equal("Sala 204", created.Description);
    }

    [Fact]
    public async Task Create_ConvertsTimesWithOffsetToUtc()
    {
        var local = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Local);

        var created = await CreateAsync(Timed(Mon23) with { StartsAt = local, EndsAt = local.AddHours(1) });

        Assert.Equal(local.ToUniversalTime(), created.StartsAt);
        Assert.Equal(DateTimeKind.Utc, created.StartsAt!.Value.Kind);
    }

    [Fact]
    public async Task Create_Series_StoresRecurrence()
    {
        var created = await CreateWeeklyAsync(until: Jan25);

        Assert.Equal(RecurrenceFrequency.Weekly, created.Recurrence);
        Assert.Equal(Jan25, created.RecurrenceUntil);
    }

    public static TheoryData<CreateCalendarEventRequest> InvalidRequests => new()
    {
        Timed(Mon23) with { Title = "   " },
        Timed(Mon23) with { Title = new string('a', CalendarEvent.TitleMaxLength + 1) },
        Timed(Mon23) with { Description = new string('a', CalendarEvent.DescriptionMaxLength + 1) },
        Timed(Mon23) with { Color = "red" },
        Timed(Mon23) with { TimeZone = null },
        Timed(Mon23) with { TimeZone = "Central European Standard Time" },
        Timed(Mon23) with { StartsAt = null },
        Timed(Mon23) with { EndsAt = null },
        Timed(Mon23) with { EndsAt = At(Mon23, 10) },
        Timed(Mon23) with { StartsAt = new DateTime(2026, 11, 23, 9, 0, 0, DateTimeKind.Unspecified) },
        Timed(Mon23) with { RecurrenceUntil = Mon30 },
        Timed(Mon23, recurrence: RecurrenceFrequency.Weekly, until: Mon23.AddDays(-1)),
        Timed(Mon23) with { Recurrence = (RecurrenceFrequency)99 },
        new("Egzamin", null, null, true, null, null, null, null, Warsaw, null, null),
        new("Egzamin", null, null, true, null, null, Mon23, Mon23.AddDays(-1), Warsaw, null, null),
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Create_RejectsInvalidInput(CreateCalendarEventRequest request)
    {
        var result = await _service.CreateAsync(request, CancellationToken.None);

        Assert.Equal(CalendarErrorType.Validation, result.Error!.Type);
        Assert.Empty(_events.Events);
    }

    [Fact]
    public async Task GetById_ReturnsOwnEvent()
    {
        var created = await CreateAsync(Timed(Mon23));

        var result = await _service.GetByIdAsync(created.Id, CancellationToken.None);

        Assert.Equal(created, result.Value);
    }

    [Fact]
    public async Task OtherUsersEvent_IsNotFound()
    {
        var created = await CreateAsync(Timed(Mon23));
        _currentUser.UserId = StrangerId;

        Assert.Equal(CalendarErrorType.NotFound, (await _service.GetByIdAsync(created.Id, CancellationToken.None)).Error!.Type);
        Assert.Equal(CalendarErrorType.NotFound, (await UpdateAsync(created.Id, Edit(Mon23, 12, 1))).Error!.Type);
        Assert.Equal(CalendarErrorType.NotFound, (await DeleteAsync(created.Id)).Error!.Type);
        Assert.Single(_events.Events);
    }

    // ---------- Wydarzenie jednorazowe ----------

    [Fact]
    public async Task Update_SingleEvent_ChangesItAndBumpsVersion()
    {
        var created = await CreateAsync(Timed(Mon23));
        _time.Advance(TimeSpan.FromHours(1));

        var result = await UpdateAsync(created.Id, Edit(Mon23, 12, created.Version, title: "Konsultacje"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Konsultacje", result.Value!.Title);
        Assert.Equal(At(Mon23, 12), result.Value.StartsAt);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal(created.CreatedAt.AddHours(1), result.Value.UpdatedAt);
    }

    [Fact]
    public async Task Update_SingleEvent_WithoutChanges_KeepsVersion()
    {
        var created = await CreateAsync(Timed(Mon23));

        var result = await UpdateAsync(created.Id, Edit(Mon23, 10, created.Version));

        Assert.Equal(1, result.Value!.Version);
    }

    [Fact]
    public async Task Update_SingleEvent_StaleVersion_ReturnsConflictWithCurrent()
    {
        var created = await CreateAsync(Timed(Mon23));
        await UpdateAsync(created.Id, Edit(Mon23, 12, 1));

        var result = await UpdateAsync(created.Id, Edit(Mon23, 14, 1));

        Assert.Equal(CalendarErrorType.Conflict, result.Error!.Type);
        Assert.Equal(2, result.Error.Current!.Version);
        Assert.Equal(At(Mon23, 12), Stored(created.Id).StartsAt);
    }

    [Fact]
    public async Task Update_ConcurrentSave_ReturnsConflict()
    {
        var created = await CreateAsync(Timed(Mon23));
        _events.FailNextSave = true;

        var result = await UpdateAsync(created.Id, Edit(Mon23, 12, 1));

        Assert.Equal(CalendarErrorType.Conflict, result.Error!.Type);
        Assert.NotNull(result.Error.Current);
    }

    [Theory]
    [InlineData(CalendarEditScope.Single)]
    [InlineData(CalendarEditScope.Following)]
    public async Task Update_SingleEvent_RejectsOccurrenceScope(CalendarEditScope scope)
    {
        var created = await CreateAsync(Timed(Mon23));

        var result = await UpdateAsync(created.Id, Edit(Mon23, 12, 1, scope, Mon23));

        Assert.Equal(CalendarErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Update_SingleEvent_CanBecomeSeries()
    {
        var created = await CreateAsync(Timed(Mon23));

        var result = await UpdateAsync(created.Id, Edit(Mon23, 10, 1, recurrence: RecurrenceFrequency.Daily));

        Assert.Equal(RecurrenceFrequency.Daily, result.Value!.Recurrence);
    }

    [Fact]
    public async Task Update_Series_RequiresScope()
    {
        var series = await CreateWeeklyAsync();

        var result = await UpdateAsync(series.Id, Edit(Mon23, 12, 1, recurrence: RecurrenceFrequency.Weekly));

        Assert.Equal(CalendarErrorType.Validation, result.Error!.Type);
    }

    // ---------- „Tylko to wydarzenie” ----------

    [Fact]
    public async Task UpdateSingle_CreatesExceptionAndKeepsSeries()
    {
        var series = await CreateWeeklyAsync();

        var result = await UpdateAsync(series.Id, EditSeries(Mon30, 12, 0, CalendarEditScope.Single, Mon30, "Wykład – sala 10"));

        Assert.True(result.IsSuccess);
        var exception = ExceptionOn(series.Id, Mon30)!;
        Assert.Equal(exception.Id, result.Value!.Id);
        Assert.Equal(series.Id, result.Value.SeriesId);
        Assert.Equal(Mon30, result.Value.OriginalDate);
        Assert.Equal("Wykład – sala 10", exception.Title);
        Assert.Equal(At(Mon30, 12), exception.StartsAt);
        Assert.Equal("Wykład", Stored(series.Id).Title);
        Assert.Equal(1, Stored(series.Id).Version);
    }

    [Fact]
    public async Task UpdateSingle_ExistingException_ComparesWithItsOwnVersion()
    {
        var series = await CreateWeeklyAsync();
        await UpdateAsync(series.Id, EditSeries(Mon30, 12, 0, CalendarEditScope.Single, Mon30)); // wyjątek: wersja 1

        var current = await UpdateAsync(series.Id, EditSeries(Mon30, 13, 1, CalendarEditScope.Single, Mon30));
        var stale = await UpdateAsync(series.Id, EditSeries(Mon30, 14, 1, CalendarEditScope.Single, Mon30));

        Assert.True(current.IsSuccess);
        Assert.Equal(2, current.Value!.Version);
        Assert.Equal(CalendarErrorType.Conflict, stale.Error!.Type);
        Assert.Equal(Mon30, stale.Error.Current!.OriginalDate);
        Assert.Equal(At(Mon30, 13), ExceptionOn(series.Id, Mon30)!.StartsAt);
    }

    [Fact]
    public async Task UpdateSingle_SecondEditOfUnchangedOccurrence_IsConflictNotOverwrite()
    {
        var series = await CreateWeeklyAsync();

        // Dwie karty widziały to samo wystąpienie bez zmian (wersja 0); wyjątek i seria zaczynają od wersji 1,
        // więc bez osobnej wersji 0 druga zmiana nadpisałaby pierwszą po cichu.
        var first = await UpdateAsync(series.Id, EditSeries(Mon30, 12, 0, CalendarEditScope.Single, Mon30, "Pierwsza karta"));
        var second = await UpdateAsync(series.Id, EditSeries(Mon30, 14, 0, CalendarEditScope.Single, Mon30, "Druga karta"));

        Assert.True(first.IsSuccess);
        Assert.Equal(CalendarErrorType.Conflict, second.Error!.Type);
        Assert.Equal("Pierwsza karta", second.Error.Current!.Title);
        Assert.Equal("Pierwsza karta", ExceptionOn(series.Id, Mon30)!.Title);
    }

    [Fact]
    public async Task UpdateSingle_ExceptionThatNoLongerExists_IsConflict()
    {
        var series = await CreateWeeklyAsync();

        // Klient widział zmienione wystąpienie (wersja 1), ale wyjątku już nie ma.
        var result = await UpdateAsync(series.Id, EditSeries(Mon30, 12, 1, CalendarEditScope.Single, Mon30));

        Assert.Equal(CalendarErrorType.Conflict, result.Error!.Type);
        Assert.Equal(series.Id, result.Error.Current!.Id);
        Assert.Single(_events.Events);
    }

    [Fact]
    public async Task UpdateSingle_WithoutChanges_CreatesNoException()
    {
        var series = await CreateWeeklyAsync();

        var result = await UpdateAsync(series.Id, EditSeries(Mon30, 10, 0, CalendarEditScope.Single, Mon30));

        Assert.True(result.IsSuccess);
        Assert.Single(_events.Events);
    }

    [Fact]
    public async Task UpdateSingle_DayThatIsNotOccurrence_IsNotFound()
    {
        var series = await CreateWeeklyAsync();

        var result = await UpdateAsync(series.Id, EditSeries(Mon30, 12, 0, CalendarEditScope.Single, Mon30.AddDays(1)));

        Assert.Equal(CalendarErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task UpdateSingle_CancelledOccurrence_IsNotFound()
    {
        var series = await CreateWeeklyAsync();
        await DeleteAsync(series.Id, CalendarEditScope.Single, Mon30);

        var result = await UpdateAsync(series.Id, EditSeries(Mon30, 12, 0, CalendarEditScope.Single, Mon30));

        Assert.Equal(CalendarErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task UpdateSingle_RequiresOccurrenceDateAndNoRecurrence()
    {
        var series = await CreateWeeklyAsync();

        var withoutDate = await UpdateAsync(series.Id, EditSeries(Mon30, 12, 0, CalendarEditScope.Single, null));
        var withRecurrence = await UpdateAsync(series.Id, Edit(Mon30, 12, 0, CalendarEditScope.Single, Mon30, recurrence: RecurrenceFrequency.Daily));

        Assert.Equal(CalendarErrorType.Validation, withoutDate.Error!.Type);
        Assert.Equal(CalendarErrorType.Validation, withRecurrence.Error!.Type);
    }

    [Fact]
    public async Task UpdateSingle_ExceptionSavedElsewhere_ReturnsConflict()
    {
        var series = await CreateWeeklyAsync();
        _events.ConflictOnNextAdd = true;

        var result = await UpdateAsync(series.Id, EditSeries(Mon30, 12, 0, CalendarEditScope.Single, Mon30));

        Assert.Equal(CalendarErrorType.Conflict, result.Error!.Type);
    }

    // ---------- „To i następne” ----------

    [Fact]
    public async Task UpdateFollowing_EndsSeriesAndStartsNewOne()
    {
        var series = await CreateWeeklyAsync(until: Jan25);
        await UpdateAsync(series.Id, EditSeries(Mon30, 12, 0, CalendarEditScope.Single, Mon30));
        await UpdateAsync(series.Id, EditSeries(Mon14, 12, 0, CalendarEditScope.Single, Mon14));

        var result = await UpdateAsync(series.Id, EditSeries(Mon7, 12, 1, CalendarEditScope.Following, Mon7, "Wykład (nowa sala)", until: Jan25));

        Assert.True(result.IsSuccess);
        var oldSeries = Stored(series.Id);
        Assert.Equal(Mon7.AddDays(-1), oldSeries.RecurrenceUntil);
        Assert.Equal(2, oldSeries.Version);

        var continuation = Stored(result.Value!.Id);
        Assert.NotEqual(series.Id, continuation.Id);
        Assert.Null(continuation.SeriesId);
        Assert.Equal("Wykład (nowa sala)", continuation.Title);
        Assert.Equal(At(Mon7, 12), continuation.StartsAt);
        Assert.Equal(RecurrenceFrequency.Weekly, continuation.Recurrence);
        Assert.Equal(Jan25, continuation.RecurrenceUntil);

        Assert.NotNull(ExceptionOn(series.Id, Mon30));
        Assert.Null(ExceptionOn(series.Id, Mon14));
    }

    [Fact]
    public async Task UpdateFollowing_FromFirstOccurrence_ChangesWholeSeries()
    {
        var series = await CreateWeeklyAsync();

        var result = await UpdateAsync(series.Id, EditSeries(Mon23, 12, 1, CalendarEditScope.Following, Mon23, "Analiza"));

        Assert.Equal(series.Id, result.Value!.Id);
        Assert.Equal("Analiza", Stored(series.Id).Title);
        Assert.Single(_events.Events);
    }

    [Fact]
    public async Task UpdateFollowing_StaleSeriesVersion_ReturnsConflict()
    {
        var series = await CreateWeeklyAsync();
        await UpdateAsync(series.Id, EditSeries(Mon23, 11, 1, CalendarEditScope.All, null));

        var result = await UpdateAsync(series.Id, EditSeries(Mon7, 12, 1, CalendarEditScope.Following, Mon7));

        Assert.Equal(CalendarErrorType.Conflict, result.Error!.Type);
        Assert.Single(_events.Events);
    }

    // ---------- „Wszystkie wydarzenia” ----------

    [Fact]
    public async Task UpdateAll_FromOccurrence_ShiftsSeriesByTheSameAmount()
    {
        var series = await CreateWeeklyAsync();
        await UpdateAsync(series.Id, EditSeries(Mon30, 12, 0, CalendarEditScope.Single, Mon30));

        // Wystąpienie z poniedziałku 7.12, 10:00 przeniesione na wtorek 8.12, 12:00–14:00.
        var result = await UpdateAsync(series.Id, EditSeries(Mon7.AddDays(1), 12, 1, CalendarEditScope.All, Mon7, hours: 2));

        Assert.True(result.IsSuccess);
        var updated = Stored(series.Id);
        Assert.Equal(At(Mon23.AddDays(1), 12), updated.StartsAt);
        Assert.Equal(At(Mon23.AddDays(1), 14), updated.EndsAt);
        Assert.Equal(2, updated.Version);

        // Wystąpienia są teraz we wtorki – wyjątek z poniedziałku nie ma już czego zastępować.
        Assert.Null(ExceptionOn(series.Id, Mon30));
    }

    [Fact]
    public async Task UpdateAll_ExceptionsKeepOnlyWhatWasChangedSeparately()
    {
        var series = await CreateWeeklyAsync();
        await UpdateAsync(series.Id, EditSeries(Mon30, 10, 0, CalendarEditScope.Single, Mon30, "Wykład – sala 10")); // zmieniony tytuł
        await UpdateAsync(series.Id, EditSeries(Mon7, 12, 0, CalendarEditScope.Single, Mon7));                      // zmieniona godzina

        var result = await UpdateAsync(series.Id, EditSeries(Mon23, 11, 1, CalendarEditScope.All, null, "Analiza"));

        Assert.True(result.IsSuccess);
        var titleChanged = ExceptionOn(series.Id, Mon30)!;
        Assert.Equal("Wykład – sala 10", titleChanged.Title);
        Assert.Equal(At(Mon30, 11), titleChanged.StartsAt);

        var timeChanged = ExceptionOn(series.Id, Mon7)!;
        Assert.Equal("Analiza", timeChanged.Title);
        Assert.Equal(At(Mon7, 12), timeChanged.StartsAt);
    }

    [Fact]
    public async Task UpdateAll_ShorterSeries_RemovesExceptionsAfterItsEnd()
    {
        var series = await CreateWeeklyAsync();
        await DeleteAsync(series.Id, CalendarEditScope.Single, Mon30);
        await UpdateAsync(series.Id, EditSeries(Mon14, 12, 0, CalendarEditScope.Single, Mon14));

        var result = await UpdateAsync(series.Id, EditSeries(Mon23, 10, 1, CalendarEditScope.All, null, until: Mon7));

        Assert.True(result.IsSuccess);
        Assert.Equal(Mon7, Stored(series.Id).RecurrenceUntil);
        Assert.True(ExceptionOn(series.Id, Mon30)!.IsCancelled);
        Assert.Null(ExceptionOn(series.Id, Mon14));
    }

    [Fact]
    public async Task UpdateAll_WithoutRecurrence_TurnsSeriesIntoSingleEvent()
    {
        var series = await CreateWeeklyAsync();
        await UpdateAsync(series.Id, EditSeries(Mon30, 12, 0, CalendarEditScope.Single, Mon30));

        var result = await UpdateAsync(series.Id, Edit(Mon23, 10, 1, CalendarEditScope.All));

        Assert.Equal(RecurrenceFrequency.None, result.Value!.Recurrence);
        Assert.Single(_events.Events);
    }

    // ---------- Usuwanie ----------

    [Fact]
    public async Task Delete_SingleEvent_RemovesIt()
    {
        var created = await CreateAsync(Timed(Mon23));

        var result = await DeleteAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_events.Events);
    }

    [Fact]
    public async Task Delete_Series_RequiresScope()
    {
        var series = await CreateWeeklyAsync();

        var result = await DeleteAsync(series.Id);

        Assert.Equal(CalendarErrorType.Validation, result.Error!.Type);
        Assert.Single(_events.Events);
    }

    [Fact]
    public async Task DeleteSingle_CancelsOnlyThatOccurrence()
    {
        var series = await CreateWeeklyAsync();

        var result = await DeleteAsync(series.Id, CalendarEditScope.Single, Mon30);

        Assert.True(result.IsSuccess);
        var cancellation = ExceptionOn(series.Id, Mon30)!;
        Assert.True(cancellation.IsCancelled);
        Assert.Equal(At(Mon30, 10), cancellation.StartsAt);
        Assert.Null(Stored(series.Id).RecurrenceUntil);
    }

    [Fact]
    public async Task DeleteSingle_ChangedOccurrence_CancelsItsException()
    {
        var series = await CreateWeeklyAsync();
        await UpdateAsync(series.Id, EditSeries(Mon30, 12, 0, CalendarEditScope.Single, Mon30));

        await DeleteAsync(series.Id, CalendarEditScope.Single, Mon30);

        var exception = Assert.Single(_events.Events, e => e.IsException());
        Assert.True(exception.IsCancelled);
        Assert.Equal(2, exception.Version);
    }

    [Fact]
    public async Task DeleteSingle_Twice_IsNotFound()
    {
        var series = await CreateWeeklyAsync();
        await DeleteAsync(series.Id, CalendarEditScope.Single, Mon30);

        var result = await DeleteAsync(series.Id, CalendarEditScope.Single, Mon30);

        Assert.Equal(CalendarErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task DeleteSingle_RequiresOccurrenceDate()
    {
        var series = await CreateWeeklyAsync();

        var result = await DeleteAsync(series.Id, CalendarEditScope.Single);

        Assert.Equal(CalendarErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task DeleteFollowing_EndsSeriesAndRemovesLaterExceptions()
    {
        var series = await CreateWeeklyAsync();
        await DeleteAsync(series.Id, CalendarEditScope.Single, Mon30);
        await UpdateAsync(series.Id, EditSeries(Mon14, 12, 0, CalendarEditScope.Single, Mon14));

        var result = await DeleteAsync(series.Id, CalendarEditScope.Following, Mon7);

        Assert.True(result.IsSuccess);
        Assert.Equal(Mon7.AddDays(-1), Stored(series.Id).RecurrenceUntil);
        Assert.NotNull(ExceptionOn(series.Id, Mon30));
        Assert.Null(ExceptionOn(series.Id, Mon14));
    }

    [Theory]
    [InlineData(CalendarEditScope.Following)]
    [InlineData(CalendarEditScope.All)]
    public async Task Delete_FromFirstOccurrenceOrAll_RemovesSeriesWithExceptions(CalendarEditScope scope)
    {
        var series = await CreateWeeklyAsync();
        await UpdateAsync(series.Id, EditSeries(Mon30, 12, 0, CalendarEditScope.Single, Mon30));

        var result = await DeleteAsync(series.Id, scope, Mon23);

        Assert.True(result.IsSuccess);
        Assert.Empty(_events.Events);
    }

    private sealed class TestUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; set; } = userId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
