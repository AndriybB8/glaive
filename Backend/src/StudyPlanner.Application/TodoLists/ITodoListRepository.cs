using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

public interface ITodoListRepository
{
    /// <summary>
    /// Listy użytkownika i listy udostępnione mu po przyjęciu zaproszenia, razem z liczbą punktów –
    /// jednym zapytaniem, bez wczytywania samych punktów.
    /// </summary>
    Task<IReadOnlyList<TodoListSummary>> GetSummariesByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Lista z dostępem użytkownika. Null, gdy listy nie ma, użytkownik nie ma do niej dostępu
    /// albo nie przyjął jeszcze zaproszenia. To jedyne miejsce, które ustala dostęp do listy.
    /// </summary>
    Task<TodoListWithAccess?> GetWithAccessAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    /// <summary>Właściciel listy i osoby po przyjęciu zaproszenia. Pusto, gdy listy nie ma.</summary>
    Task<IReadOnlyList<Guid>> GetUserIdsWithAccessAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(TodoList list, CancellationToken cancellationToken);

    /// <summary>
    /// Zapisuje zmiany listy. Rzuca <see cref="TodoListConcurrencyException"/>,
    /// gdy w międzyczasie ktoś zapisał nowszą wersję.
    /// </summary>
    Task UpdateAsync(TodoList list, CancellationToken cancellationToken);

    /// <summary>Usuwa listę. Jej punkty usuwa baza (ON DELETE CASCADE).</summary>
    Task DeleteAsync(TodoList list, CancellationToken cancellationToken);
}

/// <param name="ItemCount">Wszystkie punkty listy.</param>
/// <param name="DoneCount">Punkty oznaczone jako zrobione.</param>
/// <param name="Access">Dostęp użytkownika, dla którego pobrano listę.</param>
public sealed record TodoListSummary(TodoList List, int ItemCount, int DoneCount, TodoListAccess Access);

/// <summary>
/// Rzucany przez repozytorium przy konflikcie wersji
/// (w EF Core: przy przechwyceniu DbUpdateConcurrencyException).
/// </summary>
public sealed class TodoListConcurrencyException() : Exception("Lista została zmieniona przez inny zapis.");
