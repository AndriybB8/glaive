using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

public interface ITodoItemRepository
{
    Task<IReadOnlyList<TodoItem>> GetByListAsync(Guid listId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Punkty listy do zmiany (śledzone przez kontekst) – np. przy zmianie kolejności.
    /// Zmienione punkty zapisuje <see cref="UpdateRangeAsync"/>.
    /// </summary>
    Task<IReadOnlyList<TodoItem>> GetByListForUpdateAsync(Guid listId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Zwraca punkt tylko wtedy, gdy należy do podanego użytkownika.</summary>
    Task<TodoItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    Task AddAsync(TodoItem item, CancellationToken cancellationToken);

    /// <summary>
    /// Zapisuje zmiany punktu. Rzuca <see cref="TodoItemConcurrencyException"/>,
    /// gdy w międzyczasie ktoś zapisał nowszą wersję.
    /// </summary>
    Task UpdateAsync(TodoItem item, CancellationToken cancellationToken);

    /// <summary>
    /// Zapisuje zmiany kilku punktów naraz (wszystko albo nic). Punkty muszą pochodzić z <see cref="GetByListForUpdateAsync"/>.
    /// Rzuca <see cref="TodoItemConcurrencyException"/>, gdy któryś punkt zmieniono w międzyczasie.
    /// </summary>
    Task UpdateRangeAsync(IReadOnlyList<TodoItem> items, CancellationToken cancellationToken);

    Task DeleteAsync(TodoItem item, CancellationToken cancellationToken);
}

/// <summary>
/// Rzucany przez repozytorium przy konflikcie wersji
/// (w EF Core: przy przechwyceniu DbUpdateConcurrencyException).
/// </summary>
public sealed class TodoItemConcurrencyException() : Exception("Punkt listy został zmieniony przez inny zapis.");
