using StudyPlanner.Application.Common;
using StudyPlanner.Domain.Calendar;

namespace StudyPlanner.Application.Calendar;

/// <summary>
/// Własne wydarzenia kalendarza. Widzi je i zmienia tylko właściciel – cudze wydarzenie to brak wydarzenia (404).
/// Serie zmienia się i usuwa jak w Google Calendar: tylko to wystąpienie ("single"), to i następne ("following")
/// albo wszystkie ("all").
/// </summary>
public sealed class CalendarEventService(
    ICalendarEventRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Wersja wystąpienia serii, którego nie zmieniano osobno (nie ma jeszcze wyjątku). Wyjątki zaczynają od 1,
    /// więc dwie równoczesne zmiany tego samego wystąpienia nie nadpiszą się po cichu – druga dostanie konflikt.
    /// </summary>
    public const int UnchangedOccurrenceVersion = 0;

    private static readonly CalendarError NotFound = new(CalendarErrorType.NotFound, "Nie znaleziono wydarzenia.");
    private static readonly CalendarError OccurrenceNotFound = new(CalendarErrorType.NotFound, "Nie znaleziono tego wystąpienia wydarzenia.");
    private static readonly CalendarError OccurrenceChanged = new(CalendarErrorType.Conflict, "To wystąpienie zostało w międzyczasie zmienione w innym miejscu.");
    private static readonly CalendarError ScopeRequired = Invalid("Wydarzenie się powtarza – wybierz, które wystąpienia zmienić: \"single\", \"following\" albo \"all\".");
    private static readonly CalendarError ScopeNotAllowed = Invalid("Wydarzenie się nie powtarza – zmienia się je w całości, bez wyboru wystąpień.");
    private static readonly CalendarError OccurrenceDateRequired = Invalid("Podaj dzień wystąpienia (occurrenceDate).");

    public async Task<CalendarResult<CalendarEventDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var calendarEvent = await FindAsync(id, cancellationToken);

        return calendarEvent is null ? Failure(NotFound) : Success(calendarEvent);
    }

    public async Task<CalendarResult<CalendarEventDto>> CreateAsync(CreateCalendarEventRequest request, CancellationToken cancellationToken)
    {
        var (input, error) = Parse(EventFields.From(request));
        if (input is null)
        {
            return Failure(error!);
        }

        var calendarEvent = CalendarEvent.Create(
            currentUser.UserId,
            input.Title,
            input.Description,
            input.Color,
            input.Time,
            input.TimeZone,
            input.Recurrence,
            input.RecurrenceUntil,
            UtcNow());

        await repository.AddAsync(calendarEvent, cancellationToken);

        return Success(calendarEvent);
    }

    public async Task<CalendarResult<CalendarEventDto>> UpdateAsync(Guid id, UpdateCalendarEventRequest request, CancellationToken cancellationToken)
    {
        var (input, error) = Parse(EventFields.From(request));
        if (input is null)
        {
            return Failure(error!);
        }

        var calendarEvent = await FindAsync(id, cancellationToken);
        if (calendarEvent is null)
        {
            return Failure(NotFound);
        }

        if (!calendarEvent.IsRecurring())
        {
            return request.Scope is CalendarEditScope.Single or CalendarEditScope.Following
                ? Failure(ScopeNotAllowed)
                : await UpdateSingleEventAsync(calendarEvent, input, request.Version, cancellationToken);
        }

        return request.Scope switch
        {
            null => Failure(ScopeRequired),
            CalendarEditScope.Single => await UpdateOccurrenceAsync(calendarEvent, input, request, cancellationToken),
            CalendarEditScope.Following => await UpdateFollowingAsync(calendarEvent, input, request, cancellationToken),
            CalendarEditScope.All => await UpdateSeriesAsync(calendarEvent, input, request, cancellationToken),
            _ => Failure(Invalid("Nieznany zakres zmiany.")),
        };
    }

    /// <summary>Usuwa wydarzenie; w serii – wskazane wystąpienia (<paramref name="scope"/>), jak przy zmianie.</summary>
    public async Task<CalendarResult<bool>> DeleteAsync(
        Guid id,
        CalendarEditScope? scope,
        DateOnly? occurrenceDate,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await FindAsync(id, cancellationToken);
        if (calendarEvent is null)
        {
            return CalendarResult<bool>.Failure(NotFound);
        }

        CalendarError? error;

        if (!calendarEvent.IsRecurring())
        {
            error = scope is CalendarEditScope.Single or CalendarEditScope.Following
                ? ScopeNotAllowed
                : await DeleteWholeAsync(calendarEvent, cancellationToken);
        }
        else
        {
            error = scope switch
            {
                null => ScopeRequired,
                CalendarEditScope.Single => await CancelOccurrenceAsync(calendarEvent, occurrenceDate, cancellationToken),
                CalendarEditScope.Following => await DeleteFollowingAsync(calendarEvent, occurrenceDate, cancellationToken),
                CalendarEditScope.All => await DeleteWholeAsync(calendarEvent, cancellationToken),
                _ => Invalid("Nieznany zakres usuwania."),
            };
        }

        return error is null ? CalendarResult<bool>.Success(true) : CalendarResult<bool>.Failure(error);
    }

    private async Task<CalendarResult<CalendarEventDto>> UpdateSingleEventAsync(
        CalendarEvent calendarEvent,
        EventInput input,
        int version,
        CancellationToken cancellationToken)
    {
        if (calendarEvent.Version != version)
        {
            return Failure(Conflict(calendarEvent));
        }

        // Zapis bez zmian nie podbija wersji ani daty.
        if (input.Matches(calendarEvent))
        {
            return Success(calendarEvent);
        }

        calendarEvent.Update(
            input.Title,
            input.Description,
            input.Color,
            input.Time,
            input.TimeZone,
            input.Recurrence,
            input.RecurrenceUntil,
            UtcNow());

        try
        {
            await repository.UpdateAsync(calendarEvent, cancellationToken);
        }
        catch (CalendarEventConcurrencyException)
        {
            return Failure(await CurrentConflictAsync(calendarEvent.Id, cancellationToken));
        }

        return Success(calendarEvent);
    }

    /// <summary>„Tylko to wydarzenie”: zmienia istniejący wyjątek albo tworzy nowy dla tego wystąpienia.</summary>
    private async Task<CalendarResult<CalendarEventDto>> UpdateOccurrenceAsync(
        CalendarEvent series,
        EventInput input,
        UpdateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OccurrenceDate is not { } date)
        {
            return Failure(OccurrenceDateRequired);
        }

        if (input.Recurrence != RecurrenceFrequency.None)
        {
            return Failure(Invalid("Pojedyncze wystąpienie nie może mieć własnego powtarzania – wybierz \"following\" albo \"all\"."));
        }

        if (!CalendarEventOccurrences.IsOccurrence(series, date))
        {
            return Failure(OccurrenceNotFound);
        }

        var exceptions = await repository.GetExceptionsAsync(series.Id, cancellationToken);
        var existing = exceptions.FirstOrDefault(e => e.OriginalDate == date);

        if (existing is not null)
        {
            if (existing.IsCancelled)
            {
                return Failure(OccurrenceNotFound);
            }

            if (existing.Version != request.Version)
            {
                return Failure(Conflict(existing));
            }

            if (input.MatchesOccurrence(existing.Title, existing.Description, existing.Color, existing.GetTime()))
            {
                return Success(existing);
            }

            existing.UpdateOccurrence(input.Title, input.Description, input.Color, input.Time, UtcNow());

            try
            {
                await repository.UpdateAsync(existing, cancellationToken);
            }
            catch (CalendarEventConcurrencyException)
            {
                return Failure(await OccurrenceConflictAsync(series.Id, date, cancellationToken));
            }

            return Success(existing);
        }

        // Klient widział wystąpienie bez osobnych zmian (wersja 0). Inna wersja znaczy, że wyjątek, który widział,
        // już nie istnieje (np. usunięty przy zmianie całej serii).
        if (request.Version != UnchangedOccurrenceVersion)
        {
            return Failure(Conflict(series));
        }

        // Wystąpienie bez zmian nie potrzebuje wyjątku.
        if (input.MatchesOccurrence(series.Title, series.Description, series.Color, CalendarEventOccurrences.TimeOn(series, date)))
        {
            return Success(series);
        }

        var exception = CalendarEvent.CreateException(series, date, input.Title, input.Description, input.Color, input.Time, UtcNow());

        try
        {
            await repository.AddAsync(exception, cancellationToken);
        }
        catch (CalendarOccurrenceConflictException)
        {
            return Failure(OccurrenceChanged);
        }

        return Success(exception);
    }

    /// <summary>
    /// „To i następne”: seria kończy się dzień wcześniej, a od tego wystąpienia zaczyna się nowa seria z danymi z żądania
    /// (bez powtarzania – zwykłe wydarzenie). Wyjątki od tego dnia znikają, bo dotyczyły starej serii.
    /// </summary>
    private async Task<CalendarResult<CalendarEventDto>> UpdateFollowingAsync(
        CalendarEvent series,
        EventInput input,
        UpdateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OccurrenceDate is not { } date)
        {
            return Failure(OccurrenceDateRequired);
        }

        if (!CalendarEventOccurrences.IsOccurrence(series, date))
        {
            return Failure(OccurrenceNotFound);
        }

        // Od pierwszego wystąpienia „to i następne” to cała seria.
        if (date == series.LocalStartDate())
        {
            return await UpdateSeriesAsync(series, input, request, cancellationToken);
        }

        if (series.Version != request.Version)
        {
            return Failure(Conflict(series));
        }

        var exceptions = await repository.GetExceptionsAsync(series.Id, cancellationToken);
        var removed = exceptions.Where(e => e.OriginalDate >= date).ToList();
        var now = UtcNow();

        series.EndRecurrenceBefore(date, now);
        var continuation = CalendarEvent.Create(
            series.UserId,
            input.Title,
            input.Description,
            input.Color,
            input.Time,
            input.TimeZone,
            input.Recurrence,
            input.RecurrenceUntil,
            now);

        try
        {
            await repository.SaveSeriesAsync(series, continuation, removed, cancellationToken);
        }
        catch (CalendarEventConcurrencyException)
        {
            return Failure(await CurrentConflictAsync(series.Id, cancellationToken));
        }

        return Success(continuation);
    }

    /// <summary>
    /// „Wszystkie wydarzenia”. Gdy żądanie dotyczy konkretnego wystąpienia (<see cref="UpdateCalendarEventRequest.OccurrenceDate"/>),
    /// seria przesuwa się o tyle, o ile przesunięto to wystąpienie, i przyjmuje jego godzinę oraz długość.
    /// Wyjątki: tytuł, opis, kolor i czas, których nie zmieniano osobno, idą za serią; wyjątki dni, które przestały być
    /// wystąpieniami (np. po zmianie dnia startu albo powtarzania), są usuwane.
    /// </summary>
    private async Task<CalendarResult<CalendarEventDto>> UpdateSeriesAsync(
        CalendarEvent series,
        EventInput input,
        UpdateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        if (series.Version != request.Version)
        {
            return Failure(Conflict(series));
        }

        if (request.OccurrenceDate is { } date)
        {
            if (!CalendarEventOccurrences.IsOccurrence(series, date))
            {
                return Failure(OccurrenceNotFound);
            }

            input = input with { Time = ShiftSeriesStart(series, date, input.Time, input.Zone) };

            if (input.RecurrenceUntil < input.Time.StartDateIn(input.Zone))
            {
                return Failure(Invalid("Powtarzanie nie może kończyć się przed pierwszym wystąpieniem."));
            }
        }

        if (input.Matches(series))
        {
            return Success(series);
        }

        var exceptions = await repository.GetExceptionsAsync(series.Id, cancellationToken);

        // Przed zmianą serii: które wyjątki mają jeszcze czas wystąpienia z serii (zmieniono w nich tylko np. tytuł).
        var followingSeriesTime = exceptions
            .Where(e => !e.IsCancelled && e.GetTime() == CalendarEventOccurrences.TimeOn(series, e.OriginalDate!.Value))
            .ToHashSet();
        var (oldTitle, oldDescription, oldColor) = (series.Title, series.Description, series.Color);
        var now = UtcNow();

        series.Update(
            input.Title,
            input.Description,
            input.Color,
            input.Time,
            input.TimeZone,
            input.Recurrence,
            input.RecurrenceUntil,
            now);

        var removed = new List<CalendarEvent>();

        foreach (var exception in exceptions)
        {
            var originalDate = exception.OriginalDate!.Value;

            if (!CalendarEventOccurrences.IsOccurrence(series, originalDate))
            {
                removed.Add(exception);
                continue;
            }

            if (exception.IsCancelled)
            {
                continue;
            }

            var title = exception.Title == oldTitle ? series.Title : exception.Title;
            var description = exception.Description == oldDescription ? series.Description : exception.Description;
            var color = exception.Color == oldColor ? series.Color : exception.Color;
            var time = followingSeriesTime.Contains(exception)
                ? CalendarEventOccurrences.TimeOn(series, originalDate)
                : exception.GetTime();

            if (title != exception.Title
                || description != exception.Description
                || color != exception.Color
                || time != exception.GetTime())
            {
                exception.UpdateOccurrence(title, description, color, time, now);
            }
        }

        try
        {
            await repository.SaveSeriesAsync(series, null, removed, cancellationToken);
        }
        catch (CalendarEventConcurrencyException)
        {
            return Failure(await CurrentConflictAsync(series.Id, cancellationToken));
        }

        return Success(series);
    }

    private async Task<CalendarError?> DeleteWholeAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        try
        {
            await repository.DeleteAsync(calendarEvent, cancellationToken);
            return null;
        }
        catch (CalendarEventConcurrencyException)
        {
            return await CurrentConflictAsync(calendarEvent.Id, cancellationToken);
        }
    }

    /// <summary>„Usuń tylko to wydarzenie”: wystąpienie zostaje odwołane (wyjątek z <see cref="CalendarEvent.IsCancelled"/>).</summary>
    private async Task<CalendarError?> CancelOccurrenceAsync(CalendarEvent series, DateOnly? occurrenceDate, CancellationToken cancellationToken)
    {
        if (occurrenceDate is not { } date)
        {
            return OccurrenceDateRequired;
        }

        if (!CalendarEventOccurrences.IsOccurrence(series, date))
        {
            return OccurrenceNotFound;
        }

        var exceptions = await repository.GetExceptionsAsync(series.Id, cancellationToken);
        var existing = exceptions.FirstOrDefault(e => e.OriginalDate == date);

        if (existing is not null)
        {
            if (existing.IsCancelled)
            {
                return OccurrenceNotFound;
            }

            existing.Cancel(UtcNow());

            try
            {
                await repository.UpdateAsync(existing, cancellationToken);
                return null;
            }
            catch (CalendarEventConcurrencyException)
            {
                return await OccurrenceConflictAsync(series.Id, date, cancellationToken);
            }
        }

        var cancellation = CalendarEvent.CreateCancellation(series, date, CalendarEventOccurrences.TimeOn(series, date), UtcNow());

        try
        {
            await repository.AddAsync(cancellation, cancellationToken);
            return null;
        }
        catch (CalendarOccurrenceConflictException)
        {
            return OccurrenceChanged;
        }
    }

    /// <summary>„Usuń to i następne”: seria kończy się dzień wcześniej (od pierwszego wystąpienia – znika cała).</summary>
    private async Task<CalendarError?> DeleteFollowingAsync(CalendarEvent series, DateOnly? occurrenceDate, CancellationToken cancellationToken)
    {
        if (occurrenceDate is not { } date)
        {
            return OccurrenceDateRequired;
        }

        if (!CalendarEventOccurrences.IsOccurrence(series, date))
        {
            return OccurrenceNotFound;
        }

        if (date == series.LocalStartDate())
        {
            return await DeleteWholeAsync(series, cancellationToken);
        }

        var exceptions = await repository.GetExceptionsAsync(series.Id, cancellationToken);
        var removed = exceptions.Where(e => e.OriginalDate >= date).ToList();

        series.EndRecurrenceBefore(date, UtcNow());

        try
        {
            await repository.SaveSeriesAsync(series, null, removed, cancellationToken);
            return null;
        }
        catch (CalendarEventConcurrencyException)
        {
            return await CurrentConflictAsync(series.Id, cancellationToken);
        }
    }

    /// <summary>
    /// Czas serii po zmianie „wszystkich” z poziomu wystąpienia <paramref name="occurrenceDate"/>: pierwsze wystąpienie
    /// przesuwa się o tyle dni, o ile przesunięto to wystąpienie, i dostaje jego godzinę oraz długość.
    /// </summary>
    private static CalendarEventTime ShiftSeriesStart(
        CalendarEvent series,
        DateOnly occurrenceDate,
        CalendarEventTime occurrenceTime,
        TimeZoneInfo zone)
    {
        var dayShift = occurrenceTime.StartDateIn(zone).DayNumber - occurrenceDate.DayNumber;
        var startDate = series.LocalStartDate().AddDays(dayShift);

        if (occurrenceTime.IsAllDay)
        {
            var span = occurrenceTime.EndDate!.Value.DayNumber - occurrenceTime.StartDate!.Value.DayNumber;
            return CalendarEventTime.AllDay(startDate, startDate.AddDays(span));
        }

        var localTime = CalendarTimeZones.ToLocalTime(occurrenceTime.StartsAt!.Value, zone);
        var startsAt = CalendarTimeZones.ToUtc(startDate.ToDateTime(localTime), zone);

        return CalendarEventTime.Timed(startsAt, startsAt + (occurrenceTime.EndsAt!.Value - occurrenceTime.StartsAt!.Value));
    }

    private async Task<CalendarEvent?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var calendarEvent = await repository.GetAsync(id, cancellationToken);

        return calendarEvent is not null && calendarEvent.UserId == currentUser.UserId ? calendarEvent : null;
    }

    /// <summary>Po konflikcie zapisu: aktualna wersja z bazy (repozytorium zapomina nieaktualne dane).</summary>
    private async Task<CalendarError> CurrentConflictAsync(Guid id, CancellationToken cancellationToken)
    {
        var current = await FindAsync(id, cancellationToken);
        return current is null ? NotFound : Conflict(current);
    }

    private async Task<CalendarError> OccurrenceConflictAsync(Guid seriesId, DateOnly date, CancellationToken cancellationToken)
    {
        var exceptions = await repository.GetExceptionsAsync(seriesId, cancellationToken);
        var current = exceptions.FirstOrDefault(e => e.OriginalDate == date && !e.IsCancelled);
        return current is null ? OccurrenceNotFound : Conflict(current);
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static CalendarResult<CalendarEventDto> Success(CalendarEvent calendarEvent) =>
        CalendarResult<CalendarEventDto>.Success(CalendarEventDto.From(calendarEvent));

    private static CalendarResult<CalendarEventDto> Failure(CalendarError error) => CalendarResult<CalendarEventDto>.Failure(error);

    private static CalendarError Conflict(CalendarEvent current) =>
        new(CalendarErrorType.Conflict, "Wydarzenie zostało w międzyczasie zmienione w innym miejscu.", CalendarEventDto.From(current));

    private static CalendarError Invalid(string message) => new(CalendarErrorType.Validation, message);

    /// <summary>
    /// Sprawdza i porządkuje pola z żądania (przycięty tytuł, brak opisu = null, domyślny kolor, godziny w UTC,
    /// kanoniczna nazwa strefy). Błędy wracają jako czytelne komunikaty walidacji, zanim zrobi to encja.
    /// </summary>
    private static (EventInput? Input, CalendarError? Error) Parse(EventFields fields)
    {
        var title = fields.Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
        {
            return (null, Invalid("Podaj tytuł wydarzenia."));
        }

        if (title.Length > CalendarEvent.TitleMaxLength)
        {
            return (null, Invalid($"Tytuł może mieć maksymalnie {CalendarEvent.TitleMaxLength} znaków."));
        }

        var description = fields.Description?.Trim();
        if (string.IsNullOrEmpty(description))
        {
            description = null;
        }
        else if (description.Length > CalendarEvent.DescriptionMaxLength)
        {
            return (null, Invalid($"Opis może mieć maksymalnie {CalendarEvent.DescriptionMaxLength} znaków."));
        }

        var color = string.IsNullOrWhiteSpace(fields.Color) ? CalendarEvent.DefaultColor : fields.Color.Trim();
        if (!CalendarEvent.IsValidColor(color))
        {
            return (null, Invalid("Kolor musi być w formacie szesnastkowym, np. #2563EB."));
        }

        var timeZoneId = fields.TimeZone?.Trim();
        if (string.IsNullOrEmpty(timeZoneId))
        {
            return (null, Invalid("Podaj strefę czasową, np. Europe/Warsaw."));
        }

        if (!CalendarEvent.IsValidTimeZone(timeZoneId))
        {
            return (null, Invalid("Nieznana strefa czasowa – podaj identyfikator IANA, np. Europe/Warsaw."));
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        var (time, timeError) = ParseTime(fields);
        if (time is null)
        {
            return (null, timeError);
        }

        var recurrence = fields.Recurrence ?? RecurrenceFrequency.None;
        if (!Enum.IsDefined(recurrence))
        {
            return (null, Invalid("Nieznany rodzaj powtarzania."));
        }

        if (fields.RecurrenceUntil is { } until)
        {
            if (recurrence == RecurrenceFrequency.None)
            {
                return (null, Invalid("Datę końca powtarzania można podać tylko dla wydarzenia powtarzanego."));
            }

            if (until < time.StartDateIn(zone))
            {
                return (null, Invalid("Powtarzanie nie może kończyć się przed pierwszym wystąpieniem."));
            }
        }

        return (new EventInput(title, description, color, time, zone, recurrence, fields.RecurrenceUntil), null);
    }

    private static (CalendarEventTime? Time, CalendarError? Error) ParseTime(EventFields fields)
    {
        if (fields.IsAllDay == true)
        {
            if (fields.StartDate is not { } startDate)
            {
                return (null, Invalid("Podaj dzień rozpoczęcia wydarzenia."));
            }

            // Brak dnia zakończenia = wydarzenie jednodniowe.
            var endDate = fields.EndDate ?? startDate;
            if (endDate < startDate)
            {
                return (null, Invalid("Dzień zakończenia nie może być wcześniejszy niż dzień rozpoczęcia."));
            }

            return (CalendarEventTime.AllDay(startDate, endDate), null);
        }

        if (fields.StartsAt is not { } startsAt)
        {
            return (null, Invalid("Podaj początek wydarzenia."));
        }

        if (fields.EndsAt is not { } endsAt)
        {
            return (null, Invalid("Podaj koniec wydarzenia."));
        }

        // Godziny bez strefy (bez „Z” albo przesunięcia) byłyby niejednoznaczne.
        if (startsAt.Kind == DateTimeKind.Unspecified || endsAt.Kind == DateTimeKind.Unspecified)
        {
            return (null, Invalid("Podaj godziny w UTC, np. 2026-11-23T09:00:00Z."));
        }

        startsAt = startsAt.ToUniversalTime();
        endsAt = endsAt.ToUniversalTime();

        if (endsAt <= startsAt)
        {
            return (null, Invalid("Koniec wydarzenia musi być późniejszy niż jego początek."));
        }

        return (CalendarEventTime.Timed(startsAt, endsAt), null);
    }

    /// <summary>Wspólne pola żądań tworzenia i zmiany.</summary>
    private sealed record EventFields(
        string? Title,
        string? Description,
        string? Color,
        bool? IsAllDay,
        DateTime? StartsAt,
        DateTime? EndsAt,
        DateOnly? StartDate,
        DateOnly? EndDate,
        string? TimeZone,
        RecurrenceFrequency? Recurrence,
        DateOnly? RecurrenceUntil)
    {
        public static EventFields From(CreateCalendarEventRequest r) =>
            new(r.Title, r.Description, r.Color, r.IsAllDay, r.StartsAt, r.EndsAt, r.StartDate, r.EndDate, r.TimeZone, r.Recurrence, r.RecurrenceUntil);

        public static EventFields From(UpdateCalendarEventRequest r) =>
            new(r.Title, r.Description, r.Color, r.IsAllDay, r.StartsAt, r.EndsAt, r.StartDate, r.EndDate, r.TimeZone, r.Recurrence, r.RecurrenceUntil);
    }

    /// <summary>Poprawne, uporządkowane pola wydarzenia.</summary>
    private sealed record EventInput(
        string Title,
        string? Description,
        string Color,
        CalendarEventTime Time,
        TimeZoneInfo Zone,
        RecurrenceFrequency Recurrence,
        DateOnly? RecurrenceUntil)
    {
        public string TimeZone => Zone.Id;

        public bool Matches(CalendarEvent calendarEvent) =>
            MatchesOccurrence(calendarEvent.Title, calendarEvent.Description, calendarEvent.Color, calendarEvent.GetTime())
            && TimeZone == calendarEvent.TimeZone
            && Recurrence == calendarEvent.Recurrence
            && RecurrenceUntil == calendarEvent.RecurrenceUntil;

        public bool MatchesOccurrence(string title, string? description, string color, CalendarEventTime time) =>
            Title == title && Description == description && Color == color && Time == time;
    }
}
