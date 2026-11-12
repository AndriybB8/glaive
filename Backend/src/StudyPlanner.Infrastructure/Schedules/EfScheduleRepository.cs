using Microsoft.EntityFrameworkCore;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.Schedules;

public sealed class EfScheduleRepository(AppDbContext db) : IScheduleRepository
{
    public async Task<IReadOnlyList<ScheduleWithAccess>> GetAccessibleByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.Schedules
            .AsNoTracking()
            .Where(s => s.UserId == userId
                || db.ScheduleMembers.Any(m => m.ScheduleId == s.Id && m.UserId == userId && m.AcceptedAt != null))
            .Select(s => new
            {
                Schedule = s,
                Role = db.ScheduleMembers
                    .Where(m => m.ScheduleId == s.Id && m.UserId == userId && m.AcceptedAt != null)
                    .Select(m => (ScheduleMemberRole?)m.Role)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => (row.Schedule, Access: ToAccess(row.Schedule, userId, row.Role)))
            .Where(row => row.Access is not null)
            .Select(row => new ScheduleWithAccess(row.Schedule, row.Access!.Value))
            .ToList();
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

    public async Task<ScheduleWithAccess?> GetWithAccessAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var row = await db.Schedules
            .Where(s => s.Id == id)
            .Select(s => new
            {
                Schedule = s,
                Role = db.ScheduleMembers
                    .Where(m => m.ScheduleId == s.Id && m.UserId == userId && m.AcceptedAt != null)
                    .Select(m => (ScheduleMemberRole?)m.Role)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row != null && ToAccess(row.Schedule, userId, row.Role) is { } access ?
            new ScheduleWithAccess(row.Schedule, access) : null;
    }

    private static ScheduleAccess? ToAccess(Schedule schedule, Guid userId, ScheduleMemberRole? role) =>
        schedule.UserId == userId ? ScheduleAccess.Owner : role?.ToAccess();
}
