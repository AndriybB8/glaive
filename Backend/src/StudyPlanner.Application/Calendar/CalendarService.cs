using System.Globalization;
using StudyPlanner.Application.Common;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Domain.Calendar;

namespace StudyPlanner.Application.Calendar;

/// <summary>
/// Widok kalendarza: w jednym miejscu własne wydarzenia (z rozwiniętymi seriami), harmonogramy i zadania z terminem –
/// także udostępnione przez innych, z rolą użytkownika. Zmiany innych osób frontend odbiera przez ScheduleHub
/// (zdarzenie ScheduleChanged) i wtedy pobiera widok ponownie.
/// </summary>
public sealed class CalendarService(
    ICalendarEventRepository events,
    ICalendarScheduleRepository schedules,
    ICurrentUser currentUser)
{
    /// <summary>Najdłuższy zakres jednego zapytania – z zapasem na widok miesiąca (6 tygodni).</summary>
    public const int MaxRangeDays = 62;

    /// <param name="from">Pierwszy widoczny dzień.</param>
    /// <param name="to">Ostatni widoczny dzień (włącznie).</param>
    /// <param name="timeZone">Strefa IANA oglądającego – wyznacza, które godziny należą do widocznych dni.</param>
    public async Task<CalendarResult<IReadOnlyList<CalendarItemDto>>> GetAsync(
        DateOnly? from,
        DateOnly? to,
        string? timeZone,
        CancellationToken cancellationToken)
    {
        if (from is null || to is null)
        {
            return Failure(Invalid("Podaj zakres dat: from i to (yyyy-MM-dd)."));
        }

        if (to < from)
        {
            return Failure(Invalid("Koniec zakresu nie może być wcześniejszy niż jego początek."));
        }

        if (to.Value.DayNumber - from.Value.DayNumber + 1 > MaxRangeDays)
        {
            return Failure(Invalid($"Zakres może obejmować maksymalnie {MaxRangeDays} dni."));
        }

        var timeZoneId = timeZone?.Trim();
        if (string.IsNullOrEmpty(timeZoneId))
        {
            return Failure(Invalid("Podaj strefę czasową, np. Europe/Warsaw."));
        }

        if (!CalendarEvent.IsValidTimeZone(timeZoneId))
        {
            return Failure(Invalid("Nieznana strefa czasowa – podaj identyfikator IANA, np. Europe/Warsaw."));
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var window = CalendarWindow.For(from.Value, to.Value, zone);
        var userId = currentUser.UserId;

        var ownEvents = await events.GetForWindowAsync(userId, window, cancellationToken);
        var visibleSchedules = await schedules.GetSchedulesAsync(userId, window.From, window.To, cancellationToken);
        var dueTasks = await schedules.GetTasksDueAsync(userId, window.From, window.To, cancellationToken);

        IReadOnlyList<CalendarItemDto> items = EventItems(ownEvents, window)
            .Concat(visibleSchedules.Select(ScheduleItem))
            .Concat(dueTasks.Select(TaskItem))
            .OrderBy(item => StartDay(item, zone))
            .ThenBy(item => !item.IsAllDay)
            .ThenBy(item => item.StartsAt)
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToList();

        return CalendarResult<IReadOnlyList<CalendarItemDto>>.Success(items);
    }

    /// <summary>
    /// Wystąpienia własnych wydarzeń w widocznym zakresie. Wystąpienie z wyjątkiem jest zastąpione wyjątkiem
    /// (pokazanym w jego nowym czasie, także gdy przeniesiono je z innego dnia) albo pominięte, gdy je odwołano.
    /// </summary>
    private static IEnumerable<CalendarItemDto> EventItems(IReadOnlyList<CalendarEvent> loaded, CalendarWindow window)
    {
        var seriesById = loaded.Where(e => !e.IsException()).ToDictionary(e => e.Id);
        var exceptionsBySeries = loaded.Where(e => e.IsException()).ToLookup(e => e.SeriesId!.Value);

        foreach (var calendarEvent in seriesById.Values)
        {
            if (!calendarEvent.IsRecurring())
            {
                foreach (var occurrence in CalendarEventOccurrences.Overlapping(calendarEvent, window))
                {
                    yield return EventItem(calendarEvent, occurrence.Time, $"event:{calendarEvent.Id}", null, null, isException: false);
                }

                continue;
            }

            var replaced = exceptionsBySeries[calendarEvent.Id].Select(e => e.OriginalDate!.Value).ToHashSet();

            foreach (var occurrence in CalendarEventOccurrences.Overlapping(calendarEvent, window))
            {
                if (!replaced.Contains(occurrence.Date))
                {
                    yield return EventItem(calendarEvent, occurrence.Time, OccurrenceKey(calendarEvent.Id, occurrence.Date), occurrence.Date, calendarEvent, isException: false);
                }
            }
        }

        foreach (var exception in loaded.Where(e => e.IsException() && !e.IsCancelled))
        {
            var originalDate = exception.OriginalDate!.Value;

            // Wyjątek dnia, który nie jest już wystąpieniem serii, nie ma czego zastępować.
            if (!seriesById.TryGetValue(exception.SeriesId!.Value, out var series)
                || !CalendarEventOccurrences.IsOccurrence(series, originalDate))
            {
                continue;
            }

            var time = exception.GetTime();
            if (window.Overlaps(time))
            {
                yield return EventItem(exception, time, OccurrenceKey(series.Id, originalDate), originalDate, series, isException: true);
            }
        }
    }

    /// <param name="shown">Wiersz z treścią elementu: wydarzenie, seria albo wyjątek.</param>
    /// <param name="series">Seria, do której należy wystąpienie; null dla wydarzenia jednorazowego.</param>
    private static CalendarItemDto EventItem(
        CalendarEvent shown,
        CalendarEventTime time,
        string key,
        DateOnly? occurrenceDate,
        CalendarEvent? series,
        bool isException) =>
        new(
            key,
            CalendarItemKind.Event,
            series?.Id ?? shown.Id,
            shown.Title,
            shown.Description,
            time.IsAllDay,
            time.StartsAt,
            time.EndsAt,
            time.StartDate,
            time.EndDate,
            shown.Color,
            AccessRole.Owner,
            occurrenceDate,
            IsRecurring: series is not null,
            isException,
            series is not null && !isException ? CalendarEventService.UnchangedOccurrenceVersion : shown.Version,
            series?.Version,
            ScheduleId: null,
            ScheduleName: null,
            TaskStatus: null,
            TaskPriority: null);

    private static CalendarItemDto ScheduleItem(ScheduleWithAccess found) =>
        new(
            $"schedule:{found.Schedule.Id}",
            CalendarItemKind.Schedule,
            found.Schedule.Id,
            found.Schedule.Name,
            Description: null,
            IsAllDay: true,
            StartsAt: null,
            EndsAt: null,
            found.Schedule.StartDate,
            found.Schedule.EndDate,
            Color: null,
            found.Access,
            OccurrenceDate: null,
            IsRecurring: false,
            IsException: false,
            Version: null,
            SeriesVersion: null,
            found.Schedule.Id,
            found.Schedule.Name,
            TaskStatus: null,
            TaskPriority: null);

    private static CalendarItemDto TaskItem(CalendarTask found) =>
        new(
            $"task:{found.Task.Id}",
            CalendarItemKind.Task,
            found.Task.Id,
            found.Task.Title,
            found.Task.Description,
            IsAllDay: true,
            StartsAt: null,
            EndsAt: null,
            found.Task.DueDate,
            found.Task.DueDate,
            Color: null,
            found.Access,
            OccurrenceDate: null,
            IsRecurring: false,
            IsException: false,
            Version: null,
            SeriesVersion: null,
            found.Task.ScheduleId,
            found.ScheduleName,
            found.Task.Status,
            found.Task.Priority);

    private static string OccurrenceKey(Guid seriesId, DateOnly date) =>
        $"event:{seriesId}:{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    private static DateOnly StartDay(CalendarItemDto item, TimeZoneInfo zone) =>
        item.IsAllDay ? item.StartDate!.Value : CalendarTimeZones.ToLocalDate(item.StartsAt!.Value, zone);

    private static CalendarResult<IReadOnlyList<CalendarItemDto>> Failure(CalendarError error) =>
        CalendarResult<IReadOnlyList<CalendarItemDto>>.Failure(error);

    private static CalendarError Invalid(string message) => new(CalendarErrorType.Validation, message);
}
