namespace StudyPlanner.Domain.Calendar;

/// <summary>
/// Widoczny fragment kalendarza (dzień, tydzień, miesiąc) w strefie osoby, która go ogląda.
/// Elementy całodniowe porównuje się po dniach (<see cref="From"/>–<see cref="To"/>, włącznie),
/// a wydarzenia w godzinach po chwilach w UTC (<see cref="FromUtc"/> włącznie – <see cref="ToUtc"/> wyłącznie).
/// </summary>
public sealed record CalendarWindow
{
    private CalendarWindow(DateOnly from, DateOnly to, DateTime fromUtc, DateTime toUtc)
    {
        From = from;
        To = to;
        FromUtc = fromUtc;
        ToUtc = toUtc;
    }

    /// <summary>Pierwszy widoczny dzień.</summary>
    public DateOnly From { get; }

    /// <summary>Ostatni widoczny dzień (włącznie).</summary>
    public DateOnly To { get; }

    /// <summary>Północ pierwszego dnia w strefie oglądającego, w UTC.</summary>
    public DateTime FromUtc { get; }

    /// <summary>Północ dnia po ostatnim widocznym dniu w strefie oglądającego, w UTC (granica wyłączna).</summary>
    public DateTime ToUtc { get; }

    public static CalendarWindow For(DateOnly from, DateOnly to, TimeZoneInfo zone)
    {
        if (to < from)
        {
            throw new ArgumentException("Koniec zakresu nie może być wcześniejszy niż jego początek.", nameof(to));
        }

        return new CalendarWindow(
            from,
            to,
            CalendarTimeZones.ToUtc(from.ToDateTime(TimeOnly.MinValue), zone),
            CalendarTimeZones.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone));
    }

    /// <summary>Czy wydarzenie trwające w <paramref name="time"/> jest choć częściowo widoczne.</summary>
    public bool Overlaps(CalendarEventTime time) =>
        time.IsAllDay
            ? time.StartDate <= To && time.EndDate >= From
            : time.StartsAt < ToUtc && time.EndsAt > FromUtc;
}
