namespace StudyPlanner.Domain.Calendar;

/// <summary>
/// Kiedy trwa wydarzenie: w określonych godzinach (chwile w UTC) albo przez całe dni (dni kalendarzowe bez strefy czasowej,
/// jak zakres harmonogramu). Powstaje tylko przez <see cref="Timed"/> albo <see cref="AllDay"/>,
/// więc ma zawsze ustawioną dokładnie jedną parę: godziny albo dni.
/// </summary>
public sealed record CalendarEventTime
{
    private CalendarEventTime(bool isAllDay, DateTime? startsAt, DateTime? endsAt, DateOnly? startDate, DateOnly? endDate)
    {
        IsAllDay = isAllDay;
        StartsAt = startsAt;
        EndsAt = endsAt;
        StartDate = startDate;
        EndDate = endDate;
    }

    public bool IsAllDay { get; }

    /// <summary>Początek wydarzenia w godzinach (UTC). Null dla wydarzenia całodniowego.</summary>
    public DateTime? StartsAt { get; }

    /// <summary>Koniec wydarzenia w godzinach (UTC), zawsze późniejszy niż początek. Null dla wydarzenia całodniowego.</summary>
    public DateTime? EndsAt { get; }

    /// <summary>Pierwszy dzień wydarzenia całodniowego. Null dla wydarzenia w godzinach.</summary>
    public DateOnly? StartDate { get; }

    /// <summary>Ostatni dzień wydarzenia całodniowego (włącznie). Null dla wydarzenia w godzinach.</summary>
    public DateOnly? EndDate { get; }

    public static CalendarEventTime Timed(DateTime startsAt, DateTime endsAt)
    {
        EnsureUtc(startsAt, nameof(startsAt));
        EnsureUtc(endsAt, nameof(endsAt));

        if (endsAt <= startsAt)
        {
            throw new ArgumentException("Koniec wydarzenia musi być późniejszy niż jego początek.", nameof(endsAt));
        }

        return new CalendarEventTime(false, startsAt, endsAt, null, null);
    }

    public static CalendarEventTime AllDay(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("Data zakończenia nie może być wcześniejsza niż data rozpoczęcia.", nameof(endDate));
        }

        return new CalendarEventTime(true, null, null, startDate, endDate);
    }

    /// <summary>Dzień, w którym wydarzenie się zaczyna – dla wydarzenia w godzinach liczony w podanej strefie czasowej.</summary>
    public DateOnly StartDateIn(TimeZoneInfo timeZone) =>
        IsAllDay
            ? StartDate!.Value
            : DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(StartsAt!.Value, timeZone));

    private static void EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Data musi być w UTC.", paramName);
        }
    }
}
