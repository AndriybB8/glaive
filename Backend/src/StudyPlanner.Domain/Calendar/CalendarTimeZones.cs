namespace StudyPlanner.Domain.Calendar;

/// <summary>Zamiana czasu lokalnego (w danej strefie) na UTC i z powrotem, z jednoznacznym wynikiem przy zmianie czasu.</summary>
public static class CalendarTimeZones
{
    /// <summary>
    /// Czas lokalny w strefie <paramref name="zone"/> jako chwila w UTC.
    /// Godzina „przeskoczona” przy przejściu na czas letni (np. 02:30 w ostatnią niedzielę marca w Polsce) przesuwa się
    /// o długość przeskoku (03:30), a godzina występująca dwa razy przy powrocie do czasu zimowego oznacza pierwsze
    /// wystąpienie (jeszcze czas letni) – tak samo jak w Google Calendar.
    /// </summary>
    public static DateTime ToUtc(DateTime localTime, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            // Przesunięcie sprzed przeskoku: lokalna godzina liczona „po staremu” wypada tuż po przeskoku.
            var offsetBefore = zone.GetUtcOffset(local.AddDays(-1));
            return DateTime.SpecifyKind(local - offsetBefore, DateTimeKind.Utc);
        }

        if (zone.IsAmbiguousTime(local))
        {
            var daylightOffset = zone.GetAmbiguousTimeOffsets(local).Max();
            return DateTime.SpecifyKind(local - daylightOffset, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    /// <summary>Dzień kalendarzowy, w którym wypada chwila <paramref name="utc"/> w strefie <paramref name="zone"/>.</summary>
    public static DateOnly ToLocalDate(DateTime utc, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));

    /// <summary>Godzina, o której chwila <paramref name="utc"/> wypada w strefie <paramref name="zone"/>.</summary>
    public static TimeOnly ToLocalTime(DateTime utc, TimeZoneInfo zone) =>
        TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));
}
