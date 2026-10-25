using Microsoft.EntityFrameworkCore;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.Schedules;

public sealed class EfScheduleRepository(AppDbContext db) : IScheduleRepository
{
    public async Task<IReadOnlyList<Schedule>> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.Schedules
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task<Schedule?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        // Śledzony przez kontekst – ScheduleService zmienia go i zapisuje przez UpdateAsync.
        return db.Schedules.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(Schedule schedule, CancellationToken cancellationToken)
    {
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Schedule schedule, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Zapominamy nieaktualne dane, żeby kolejny odczyt pobrał wersję z bazy (pokazywaną przy konflikcie).
            db.ChangeTracker.Clear();
            throw new ScheduleConcurrencyException();
        }
    }

    public async Task DeleteAsync(Schedule schedule, CancellationToken cancellationToken)
    {
        db.Schedules.Remove(schedule);
        await db.SaveChangesAsync(cancellationToken);
    }
}
