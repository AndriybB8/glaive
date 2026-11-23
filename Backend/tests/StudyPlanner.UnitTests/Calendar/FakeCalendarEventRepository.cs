using StudyPlanner.Application.Calendar;
using StudyPlanner.Domain.Calendar;

namespace StudyPlanner.UnitTests.Calendar;

/// <summary>
/// Wydarzenia w pamięci. Encje są zmieniane w miejscu (jak śledzone przez EF), więc zapis tylko symuluje konflikty.
/// <see cref="GetForWindowAsync"/> zwraca wszystkie wydarzenia użytkownika – kontrakt pozwala na nadmiar, a wybór robi serwis.
/// </summary>
internal sealed class FakeCalendarEventRepository : ICalendarEventRepository
{
    public List<CalendarEvent> Events { get; } = [];

    /// <summary>Następny zapis zgłosi konflikt wersji (jak DbUpdateConcurrencyException).</summary>
    public bool FailNextSave { get; set; }

    /// <summary>Następne dodanie zgłosi, że wystąpienie ma już wyjątek (jak naruszenie unikalnego indeksu).</summary>
    public bool ConflictOnNextAdd { get; set; }

    public Task<CalendarEvent?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Events.FirstOrDefault(e => e.Id == id && !e.IsException()));

    public Task<IReadOnlyList<CalendarEvent>> GetExceptionsAsync(Guid seriesId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CalendarEvent>>(Events.Where(e => e.SeriesId == seriesId).ToList());

    public Task<IReadOnlyList<CalendarEvent>> GetForWindowAsync(Guid userId, CalendarWindow window, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CalendarEvent>>(Events.Where(e => e.UserId == userId).ToList());

    public Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        var duplicate = calendarEvent.IsException()
            && Events.Any(e => e.SeriesId == calendarEvent.SeriesId && e.OriginalDate == calendarEvent.OriginalDate);

        if (ConflictOnNextAdd || duplicate)
        {
            ConflictOnNextAdd = false;
            throw new CalendarOccurrenceConflictException();
        }

        Events.Add(calendarEvent);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        return Task.CompletedTask;
    }

    public Task SaveSeriesAsync(
        CalendarEvent series,
        CalendarEvent? added,
        IReadOnlyCollection<CalendarEvent> removed,
        CancellationToken cancellationToken)
    {
        ThrowIfFailing();

        if (added is not null)
        {
            Events.Add(added);
        }

        Events.RemoveAll(removed.Contains);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        ThrowIfFailing();

        // Kaskada: seria znika razem z wyjątkami.
        Events.RemoveAll(e => e == calendarEvent || e.SeriesId == calendarEvent.Id);
        return Task.CompletedTask;
    }

    private void ThrowIfFailing()
    {
        if (FailNextSave)
        {
            FailNextSave = false;
            throw new CalendarEventConcurrencyException();
        }
    }
}
