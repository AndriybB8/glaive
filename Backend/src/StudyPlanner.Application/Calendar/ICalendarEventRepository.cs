using StudyPlanner.Domain.Calendar;

namespace StudyPlanner.Application.Calendar;

public interface ICalendarEventRepository
{
    /// <summary>Wydarzenie jednorazowe albo seria (nie wyjątek), śledzone – serwis zmienia je i zapisuje.</summary>
    Task<CalendarEvent?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Wszystkie wyjątki serii (zmienione i odwołane wystąpienia), śledzone.</summary>
    Task<IReadOnlyList<CalendarEvent>> GetExceptionsAsync(Guid seriesId, CancellationToken cancellationToken);

    /// <summary>
    /// Wydarzenia użytkownika potrzebne do pokazania <paramref name="window"/> (tylko do odczytu): widoczne wydarzenia
    /// jednorazowe, serie, które mogą mieć widoczne wystąpienie, wszystkie ich wyjątki (żeby ukryć zastąpione wystąpienia)
    /// oraz wyjątki przeniesione w widoczny zakres razem z ich seriami. Dokładny wybór robi wywołujący – może dostać więcej.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> GetForWindowAsync(Guid userId, CalendarWindow window, CancellationToken cancellationToken);

    /// <summary>
    /// Zapisuje nowe wydarzenie. Rzuca <see cref="CalendarOccurrenceConflictException"/>,
    /// gdy to wystąpienie serii ma już wyjątek (zapisany w międzyczasie w innym miejscu).
    /// </summary>
    Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Zapisuje zmiany wydarzenia. Rzuca <see cref="CalendarEventConcurrencyException"/>,
    /// gdy w międzyczasie ktoś zapisał nowszą wersję.
    /// </summary>
    Task UpdateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Zapisuje razem (jedną transakcją) zmiany serii i jej śledzonych wyjątków, nowe wydarzenie <paramref name="added"/>
    /// (dalsza część serii przy „to i następne”) i usunięcie wyjątków <paramref name="removed"/>.
    /// Rzuca <see cref="CalendarEventConcurrencyException"/> przy konflikcie wersji.
    /// </summary>
    Task SaveSeriesAsync(
        CalendarEvent series,
        CalendarEvent? added,
        IReadOnlyCollection<CalendarEvent> removed,
        CancellationToken cancellationToken);

    /// <summary>
    /// Usuwa wydarzenie; seria znika razem z wyjątkami (kaskada).
    /// Rzuca <see cref="CalendarEventConcurrencyException"/>, gdy w międzyczasie ktoś zapisał nowszą wersję.
    /// </summary>
    Task DeleteAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken);
}

/// <summary>Rzucany przy konflikcie wersji (w EF Core: przy przechwyceniu DbUpdateConcurrencyException).</summary>
public sealed class CalendarEventConcurrencyException() : Exception("Wydarzenie zostało zmienione przez inny zapis.");

/// <summary>Rzucany, gdy wystąpienie serii ma już wyjątek (unikalny indeks series_id + original_date).</summary>
public sealed class CalendarOccurrenceConflictException() : Exception("To wystąpienie serii zostało już zmienione.");
