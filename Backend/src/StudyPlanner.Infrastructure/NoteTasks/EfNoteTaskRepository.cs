using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPlanner.Application.Notes;
using StudyPlanner.Application.NoteTasks;
using StudyPlanner.Domain.NoteTasks;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.NoteTasks;

public sealed class EfNoteTaskRepository(AppDbContext db) : INoteTaskRepository
{
    public Task<bool> NoteExistsAsync(Guid noteId, Guid userId, CancellationToken cancellationToken)
    {
        return db.Notes.AnyAsync(n => n.Id == noteId && n.AuthorId == userId, cancellationToken);
    }

    public Task<LinkedTaskDto?> FindTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken)
    {
        // Join z harmonogramem: potrzebna jego nazwa, a zadanie usuniętego harmonogramu traktujemy jak nieistniejące.
        var query =
            from t in db.Tasks.AsNoTracking()
            join s in db.Schedules.AsNoTracking() on t.ScheduleId equals s.Id
            where t.Id == taskId && t.UserId == userId && s.UserId == userId
            select new LinkedTaskDto(t.Id, t.ScheduleId, s.Name, t.Title, t.Status, t.DueDate);

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LinkedTaskDto>> GetTasksForNoteAsync(Guid noteId, Guid userId, CancellationToken cancellationToken)
    {
        var query =
            from l in db.NoteTaskLinks.AsNoTracking()
            join t in db.Tasks.AsNoTracking() on l.TaskId equals t.Id
            join s in db.Schedules.AsNoTracking() on t.ScheduleId equals s.Id
            where l.NoteId == noteId && t.UserId == userId && s.UserId == userId
            select new LinkedTaskDto(t.Id, t.ScheduleId, s.Name, t.Title, t.Status, t.DueDate);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NoteSummaryDto>> GetNotesForTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken)
    {
        // Projekcja bez kolumny content – podgląd treści pobiera front osobno (GET /api/notes/{id}).
        var query =
            from l in db.NoteTaskLinks.AsNoTracking()
            join n in db.Notes.AsNoTracking() on l.NoteId equals n.Id
            where l.TaskId == taskId && n.AuthorId == userId
            select new NoteSummaryDto(n.Id, n.Title, n.UpdatedAt, n.GroupId);

        return await query.ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid noteId, Guid taskId, CancellationToken cancellationToken)
    {
        return db.NoteTaskLinks.AnyAsync(l => l.NoteId == noteId && l.TaskId == taskId, cancellationToken);
    }

    public async Task<bool> AddAsync(NoteTaskLink link, CancellationToken cancellationToken)
    {
        db.NoteTaskLinks.Add(link);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Ktoś przypisał tę samą parę chwilę wcześniej – efekt jest taki, jakiego chcieliśmy.
            db.ChangeTracker.Clear();
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // Notatkę albo zadanie usunięto między sprawdzeniem a zapisem.
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task RemoveAsync(Guid noteId, Guid taskId, CancellationToken cancellationToken)
    {
        // Jedno DELETE bez wczytywania wiersza.
        await db.NoteTaskLinks
            .Where(l => l.NoteId == noteId && l.TaskId == taskId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
