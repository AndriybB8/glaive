namespace StudyPlanner.Domain.Calendar;

/// <summary>Jedno wystąpienie wydarzenia: dzień według serii (w czasie lokalnym wydarzenia) i czas trwania.</summary>
public sealed record CalendarOccurrence(DateOnly Date, CalendarEventTime Time);

/// <summary>
/// Rozwijanie serii w wystąpienia. Dni liczy się w czasie lokalnym wydarzenia (<see cref="CalendarEvent.TimeZone"/>):
/// codziennie +1 dzień, co tydzień +7 dni, co miesiąc ten sam dzień miesiąca – miesiące bez tego dnia (np. 31.) są pomijane.
/// Godzina rozpoczęcia zostaje ta sama w czasie lokalnym, więc po zmianie czasu przesuwa się w UTC; długość się nie zmienia.
/// Wyjątki serii (zmienione i odwołane wystąpienia) uwzględnia dopiero wywołujący – tu jest tylko sama reguła.
/// </summary>
public static class CalendarEventOccurrences
{
    /// <summary>
    /// Wystąpienia widoczne w <paramref name="window"/>, także te, które zaczęły się wcześniej, ale jeszcze trwają.
    /// Wydarzenie jednorazowe ma jedno wystąpienie – samo siebie.
    /// </summary>
    public static IEnumerable<CalendarOccurrence> Overlapping(CalendarEvent calendarEvent, CalendarWindow window)
    {
        EnsureNotException(calendarEvent);

        var time = calendarEvent.GetTime();

        if (!calendarEvent.IsRecurring())
        {
            if (window.Overlaps(time))
            {
                yield return new CalendarOccurrence(calendarEvent.LocalStartDate(), time);
            }

            yield break;
        }

        // Zakres dni, w których może się zacząć widoczne wystąpienie – z zapasem na długość wydarzenia.
        DateOnly firstCandidate;
        DateOnly lastCandidate;

        if (time.IsAllDay)
        {
            firstCandidate = window.From.AddDays(-SpanInDays(time));
            lastCandidate = window.To;
        }
        else
        {
            var zone = calendarEvent.GetTimeZone();
            var duration = time.EndsAt!.Value - time.StartsAt!.Value;
            firstCandidate = CalendarTimeZones.ToLocalDate(window.FromUtc - duration, zone).AddDays(-1);
            lastCandidate = CalendarTimeZones.ToLocalDate(window.ToUtc, zone);
        }

        foreach (var date in OccurrenceDates(calendarEvent, firstCandidate, lastCandidate))
        {
            var occurrenceTime = TimeOn(calendarEvent, date);

            if (window.Overlaps(occurrenceTime))
            {
                yield return new CalendarOccurrence(date, occurrenceTime);
            }
        }
    }

    /// <summary>
    /// Dni z zakresu <paramref name="from"/>–<paramref name="to"/> (włącznie), w które zaczyna się wystąpienie, rosnąco.
    /// Pierwszy kandydat jest wyliczany od razu, więc długa seria nie jest przechodzona od początku.
    /// </summary>
    public static IEnumerable<DateOnly> OccurrenceDates(CalendarEvent calendarEvent, DateOnly from, DateOnly to)
    {
        EnsureNotException(calendarEvent);

        var start = calendarEvent.LocalStartDate();

        if (!calendarEvent.IsRecurring())
        {
            if (start >= from && start <= to)
            {
                yield return start;
            }

            yield break;
        }

        var first = from > start ? from : start;
        var last = calendarEvent.RecurrenceUntil is { } until && until < to ? until : to;

        if (last < first)
        {
            yield break;
        }

        if (calendarEvent.Recurrence == RecurrenceFrequency.Monthly)
        {
            var months = Math.Max(0, ((first.Year - start.Year) * 12) + first.Month - start.Month);

            for (var date = start.AddMonths(months); date <= last; date = start.AddMonths(++months))
            {
                // AddMonths przycina do ostatniego dnia miesiąca (31.01 + 1 = 28.02) – takie dni to nie wystąpienia.
                if (date.Day == start.Day && date >= first)
                {
                    yield return date;
                }
            }

            yield break;
        }

        var step = calendarEvent.Recurrence == RecurrenceFrequency.Weekly ? 7 : 1;
        var stepsToFirst = (first.DayNumber - start.DayNumber + step - 1) / step;

        for (var date = start.AddDays(stepsToFirst * step); date <= last; date = date.AddDays(step))
        {
            yield return date;
        }
    }

    /// <summary>Czy seria ma wystąpienie zaczynające się w dniu <paramref name="date"/>. Dla wydarzenia jednorazowego zawsze false.</summary>
    public static bool IsOccurrence(CalendarEvent calendarEvent, DateOnly date) =>
        calendarEvent.IsRecurring() && OccurrenceDates(calendarEvent, date, date).Any();

    /// <summary>
    /// Czas wystąpienia zaczynającego się w dniu <paramref name="date"/>: ta sama lokalna godzina i długość co pierwsze wystąpienie.
    /// Nie sprawdza, czy to dzień wystąpienia – do tego jest <see cref="IsOccurrence"/>.
    /// </summary>
    public static CalendarEventTime TimeOn(CalendarEvent calendarEvent, DateOnly date)
    {
        EnsureNotException(calendarEvent);

        var time = calendarEvent.GetTime();

        if (date == calendarEvent.LocalStartDate())
        {
            return time;
        }

        if (time.IsAllDay)
        {
            return CalendarEventTime.AllDay(date, date.AddDays(SpanInDays(time)));
        }

        var zone = calendarEvent.GetTimeZone();
        var localStart = CalendarTimeZones.ToLocalTime(time.StartsAt!.Value, zone);
        var startsAt = CalendarTimeZones.ToUtc(date.ToDateTime(localStart), zone);

        return CalendarEventTime.Timed(startsAt, startsAt + (time.EndsAt!.Value - time.StartsAt!.Value));
    }

    private static int SpanInDays(CalendarEventTime allDay) => allDay.EndDate!.Value.DayNumber - allDay.StartDate!.Value.DayNumber;

    private static void EnsureNotException(CalendarEvent calendarEvent)
    {
        if (calendarEvent.IsException())
        {
            throw new InvalidOperationException("Wyjątek serii to pojedyncze wystąpienie – nie rozwija się go.");
        }
    }
}
