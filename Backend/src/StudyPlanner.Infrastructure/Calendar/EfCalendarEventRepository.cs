using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPlanner.Application.Calendar;
using StudyPlanner.Domain.Calendar;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.Calendar;

public sealed class EfCalendarEventRepository(AppDbContext db) : ICalendarEventRepository
{
    public Task<CalendarEvent?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id && e.SeriesId == null, cancellationToken);

    public async Task<IReadOnlyList<CalendarEvent>> GetExceptionsAsync(Guid seriesId, CancellationToken cancellationToken) =>
        await db.CalendarEvents.Where(e => e.SeriesId == seriesId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CalendarEvent>> GetForWindowAsync(Guid userId, CalendarWindow window, CancellationToken cancellationToken)
    {
        var (from, to, fromUtc, toUtc) = (window.From, window.To, window.FromUtc, window.ToUtc);
        var own = db.CalendarEvents.AsNoTracking().Where(e => e.UserId == userId);

        // Wydarzenia jednorazowe widoczne w zakresie i serie zaczynające się najpóźniej w ostatnim widocznym dniu
        // (koniec serii i długość wystąpień sprawdza dopiero rozwijanie serii).
        var events = await own
            .Where(e => e.SeriesId == null)
            .Where(e =>
                (e.Recurrence != RecurrenceFrequency.None && (e.StartDate <= to || e.StartsAt < toUtc))
                || (e.StartDate <= to && e.EndDate >= from)
                || (e.StartsAt < toUtc && e.EndsAt > fromUtc))
            .ToListAsync(cancellationToken);

        var seriesIds = events.Where(e => e.IsRecurring()).Select(e => e.Id).ToList();

        // Wszystkie wyjątki tych serii (także odwołane – ukrywają wystąpienia) oraz wyjątki przeniesione w widoczny zakres.
        var exceptions = await own
            .Where(e => e.SeriesId != null)
            .Where(e =>
                seriesIds.Contains(e.SeriesId!.Value)
                || (!e.IsCancelled
                    && ((e.StartDate <= to && e.EndDate >= from) || (e.StartsAt < toUtc && e.EndsAt > fromUtc))))
            .ToListAsync(cancellationToken);

        // Serie wyjątków przeniesionych sprzed ich początku – bez nich nie da się sprawdzić, czy wyjątek jest aktualny.
        var missingSeriesIds = exceptions.Select(e => e.SeriesId!.Value).Distinct().Except(seriesIds).ToList();
        if (missingSeriesIds.Count > 0)
        {
            events.AddRange(await own.Where(e => missingSeriesIds.Contains(e.Id)).ToListAsync(cancellationToken));
        }

        return [.. events, .. exceptions];
    }

    public async Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        db.CalendarEvents.Add(calendarEvent);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Nieudany wiersz nie może zostać w kontekście, bo kolejny zapis próbowałby dodać go jeszcze raz.
            db.Entry(calendarEvent).State = EntityState.Detached;
            throw new CalendarOccurrenceConflictException();
        }
    }

    public Task UpdateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken) => SaveAsync(cancellationToken);

    public Task SaveSeriesAsync(
        CalendarEvent series,
        CalendarEvent? added,
        IReadOnlyCollection<CalendarEvent> removed,
        CancellationToken cancellationToken)
    {
        if (added is not null)
        {
            db.CalendarEvents.Add(added);
        }

        db.CalendarEvents.RemoveRange(removed);

        // Jeden SaveChanges = jedna transakcja: seria, jej wyjątki i dalsza część zapisują się razem albo wcale.
        return SaveAsync(cancellationToken);
    }

    public Task DeleteAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        db.CalendarEvents.Remove(calendarEvent);
        return SaveAsync(cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Zapominamy nieaktualne dane, żeby kolejny odczyt pobrał wersję z bazy (pokazywaną przy konflikcie).
            db.ChangeTracker.Clear();
            throw new CalendarEventConcurrencyException();
        }
    }
}
