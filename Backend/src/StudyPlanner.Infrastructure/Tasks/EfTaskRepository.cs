using Microsoft.EntityFrameworkCore;
using StudyPlanner.Application.Tasks;
using StudyPlanner.Domain.Tasks;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.Tasks;

public sealed class EfTaskRepository(AppDbContext db) : ITaskRepository
{
    public async Task<IReadOnlyList<TaskItem>> GetByScheduleAsync(Guid scheduleId, Guid userId, CancellationToken cancellationToken)
    {
        return await db.Tasks
            .AsNoTracking()
            .Where(t => t.ScheduleId == scheduleId && t.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task<TaskItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        // Śledzony przez kontekst – TaskService zmienia go i zapisuje przez UpdateAsync.
        return db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken)
    {
        db.Tasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TaskItem task, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Zapominamy nieaktualne dane, żeby kolejny odczyt pobrał wersję z bazy (pokazywaną przy konflikcie).
            db.ChangeTracker.Clear();
            throw new TaskConcurrencyException();
        }
    }

    public async Task DeleteAsync(TaskItem task, CancellationToken cancellationToken)
    {
        db.Tasks.Remove(task);
        await db.SaveChangesAsync(cancellationToken);
    }
}
