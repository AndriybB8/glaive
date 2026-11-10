using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPlanner.Application.TodoLists;
using StudyPlanner.Domain.TodoLists;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.TodoLists;

public sealed class EfTodoListMemberRepository(AppDbContext db) : ITodoListMemberRepository
{
    public async Task<IReadOnlyList<TodoListMemberWithEmail>> GetByListAsync(Guid listId, CancellationToken cancellationToken)
    {
        return await db.TodoListMembers
            .AsNoTracking()
            .Where(m => m.ListId == listId)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new TodoListMemberWithEmail(m, u.Email))
            .ToListAsync(cancellationToken);
    }

    public Task<TodoListMemberWithEmail?> GetAsync(Guid listId, Guid userId, CancellationToken cancellationToken)
    {
        // Wpis śledzony przez kontekst – TodoListMemberService zmienia go i zapisuje przez UpdateAsync.
        return db.TodoListMembers
            .Where(m => m.ListId == listId && m.UserId == userId)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new TodoListMemberWithEmail(m, u.Email))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TodoListInvitationDto>> GetPendingByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await (
            from member in db.TodoListMembers
            where member.UserId == userId && member.AcceptedAt == null
            join list in db.TodoLists on member.ListId equals list.Id
            join owner in db.Users on list.UserId equals owner.Id
            select new TodoListInvitationDto(list.Id, list.Title, owner.Email, member.Role, member.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AddTodoListMemberResult> AddAsync(TodoListMember member, CancellationToken cancellationToken)
    {
        db.TodoListMembers.Add(member);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return AddTodoListMemberResult.Added;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return AddTodoListMemberResult.AlreadyExists;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            db.ChangeTracker.Clear();
            return AddTodoListMemberResult.ReferenceMissing;
        }
    }

    public async Task<bool> UpdateAsync(TodoListMember member, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // UPDATE nie zmienił żadnego wiersza: wpis usunięto po jego wczytaniu.
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<bool> RemoveAsync(Guid listId, Guid userId, CancellationToken cancellationToken)
    {
        // Jedno DELETE bez wczytywania wiersza.
        var removed = await db.TodoListMembers
            .Where(m => m.ListId == listId && m.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0;
    }
}
