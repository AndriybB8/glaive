using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

public interface ITodoListMemberRepository
{
    /// <summary>Osoby z dostępem do listy i oczekujące zaproszenia.</summary>
    Task<IReadOnlyList<TodoListMemberWithEmail>> GetByListAsync(Guid listId, CancellationToken cancellationToken);

    /// <summary>Wpis śledzony przez kontekst – serwis zmienia go i zapisuje przez <see cref="UpdateAsync"/>.</summary>
    Task<TodoListMemberWithEmail?> GetAsync(Guid listId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Zaproszenia użytkownika, na które jeszcze nie odpowiedział.</summary>
    Task<IReadOnlyList<TodoListInvitationDto>> GetPendingByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<AddTodoListMemberResult> AddAsync(TodoListMember member, CancellationToken cancellationToken);

    /// <summary>Zapisuje zmiany wpisu. False, gdy w międzyczasie go usunięto (np. właściciel cofnął zaproszenie).</summary>
    Task<bool> UpdateAsync(TodoListMember member, CancellationToken cancellationToken);

    /// <summary>Usuwa wpis. False, gdy go nie było.</summary>
    Task<bool> RemoveAsync(Guid listId, Guid userId, CancellationToken cancellationToken);
}

public sealed record TodoListMemberWithEmail(TodoListMember Member, string Email);

public enum AddTodoListMemberResult
{
    Added,

    /// <summary>Ta osoba ma już dostęp do listy albo zaproszenie.</summary>
    AlreadyExists,

    /// <summary>Listę albo konto usunięto między sprawdzeniem a zapisem.</summary>
    ReferenceMissing,
}
