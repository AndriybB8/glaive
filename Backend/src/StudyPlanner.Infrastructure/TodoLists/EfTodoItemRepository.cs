using Microsoft.EntityFrameworkCore;
using StudyPlanner.Application.TodoLists;
using StudyPlanner.Domain.TodoLists;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.TodoLists;

public sealed class EfTodoItemRepository(AppDbContext db) : ITodoItemRepository
{
    public async Task<IReadOnlyList<TodoItem>> GetByListAsync(Guid listId, Guid userId, CancellationToken cancellationToken)
    {
        return await db.TodoItems
            .AsNoTracking()
            .Where(i => i.ListId == listId && i.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task<TodoItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        // Śledzony przez kontekst – TodoItemService zmienia go i zapisuje przez UpdateAsync.
        return db.TodoItems.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(TodoItem item, CancellationToken cancellationToken)
    {
        db.TodoItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TodoItem item, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Zapominamy nieaktualne dane, żeby kolejny odczyt pobrał wersję z bazy (pokazywaną przy konflikcie).
            db.ChangeTracker.Clear();
            throw new TodoItemConcurrencyException();
        }
    }

    public async Task DeleteAsync(TodoItem item, CancellationToken cancellationToken)
    {
        db.TodoItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
    }
}
