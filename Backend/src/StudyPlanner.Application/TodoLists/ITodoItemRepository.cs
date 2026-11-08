using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

/// <summary>
/// Punkty list. Repozytorium nie sprawdza dostępu: listę mogą oglądać także osoby, którym ją udostępniono,
/// dlatego dostęp ustala <see cref="TodoItemService"/> przez <see cref="ITodoListRepository.GetWithAccessAsync"/>.
/// </summary>
public interface ITodoItemRepository
{
    Task<IReadOnlyList<TodoItem>> GetByListAsync(Guid listId, CancellationToken cancellationToken);

    /// <summary>
    /// Punkty listy do zmiany (śledzone przez kontekst) – np. przy zmianie kolejności.
    /// Zmienione punkty zapisuje <see cref="UpdateRangeAsync"/>.
    /// </summary>
    Task<IReadOnlyList<TodoItem>> GetByListForUpdateAsync(Guid listId, CancellationToken cancellationToken);

    Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

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
