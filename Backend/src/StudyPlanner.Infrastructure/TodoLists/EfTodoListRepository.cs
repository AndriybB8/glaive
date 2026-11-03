using Microsoft.EntityFrameworkCore;
using StudyPlanner.Application.TodoLists;
using StudyPlanner.Domain.TodoLists;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.TodoLists;

public sealed class EfTodoListRepository(AppDbContext db) : ITodoListRepository
{
    public async Task<IReadOnlyList<TodoListSummary>> GetSummariesByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Liczniki jako podzapytania w jednym SELECT – punktów nie wczytujemy.
        return await db.TodoLists
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .Select(l => new TodoListSummary(
                l,
                db.TodoItems.Count(i => i.ListId == l.Id),
                db.TodoItems.Count(i => i.ListId == l.Id && i.IsDone)))
            .ToListAsync(cancellationToken);
    }

    public Task<TodoList?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        // Śledzona przez kontekst – TodoListService zmienia ją i zapisuje przez UpdateAsync.
        return db.TodoLists.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        return db.TodoLists.AnyAsync(l => l.Id == id && l.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(TodoList list, CancellationToken cancellationToken)
    {
        db.TodoLists.Add(list);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TodoList list, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Zapominamy nieaktualne dane, żeby kolejny odczyt pobrał wersję z bazy (pokazywaną przy konflikcie).
            db.ChangeTracker.Clear();
            throw new TodoListConcurrencyException();
        }
    }

    public async Task DeleteAsync(TodoList list, CancellationToken cancellationToken)
    {
        // Punkty listy usuwa baza (ON DELETE CASCADE).
        db.TodoLists.Remove(list);
        await db.SaveChangesAsync(cancellationToken);
    }
}
