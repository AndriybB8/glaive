using Microsoft.EntityFrameworkCore;
using StudyPlanner.Application.TodoLists;
using StudyPlanner.Domain.TodoLists;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.TodoLists;

public sealed class EfTodoListRepository(AppDbContext db) : ITodoListRepository
{
    public async Task<IReadOnlyList<TodoListSummary>> GetSummariesByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Liczniki i rola jako podzapytania w jednym SELECT – punktów nie wczytujemy.
        // Dostęp daje tylko przyjęte zaproszenie (accepted_at).
        var rows = await db.TodoLists
            .AsNoTracking()
            .Where(l => l.UserId == userId
                        || db.TodoListMembers.Any(m => m.ListId == l.Id && m.UserId == userId && m.AcceptedAt != null))
            .Select(l => new
            {
                List = l,
                ItemCount = db.TodoItems.Count(i => i.ListId == l.Id),
                DoneCount = db.TodoItems.Count(i => i.ListId == l.Id && i.IsDone),
                Role = db.TodoListMembers
                    .Where(m => m.ListId == l.Id && m.UserId == userId && m.AcceptedAt != null)
                    .Select(m => (TodoListMemberRole?)m.Role)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new TodoListSummary(r.List, r.ItemCount, r.DoneCount, ToAccess(r.List, userId, r.Role)!.Value))
            .ToList();
    }

    public async Task<TodoListWithAccess?> GetWithAccessAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        // Lista śledzona przez kontekst – TodoListService zmienia ją i zapisuje przez UpdateAsync.
        var row = await db.TodoLists
            .Where(l => l.Id == id)
            .Select(l => new
            {
                List = l,
                Role = db.TodoListMembers
                    .Where(m => m.ListId == l.Id && m.UserId == userId && m.AcceptedAt != null)
                    .Select(m => (TodoListMemberRole?)m.Role)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is not null && ToAccess(row.List, userId, row.Role) is { } access
            ? new TodoListWithAccess(row.List, access)
            : null;
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
        // Punkty listy i osoby z dostępem usuwa baza (ON DELETE CASCADE).
        db.TodoLists.Remove(list);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static TodoListAccess? ToAccess(TodoList list, Guid userId, TodoListMemberRole? role)
    {
        if (list.UserId == userId)
        {
            return TodoListAccess.Owner;
        }

        return role?.ToAccess();
    }
}
