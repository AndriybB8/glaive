using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

public interface ITodoListRepository
{
    /// <summary>Listy użytkownika razem z liczbą punktów – jednym zapytaniem, bez wczytywania samych punktów.</summary>
    Task<IReadOnlyList<TodoListSummary>> GetSummariesByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Zwraca listę tylko wtedy, gdy należy do podanego użytkownika.</summary>
    Task<TodoList?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    /// <summary>Czy lista istnieje i należy do użytkownika (np. przed dodaniem do niej punktu).</summary>
    Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken cancellationToken);

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
public sealed record TodoListSummary(TodoList List, int ItemCount, int DoneCount);

/// <summary>
/// Rzucany przez repozytorium przy konflikcie wersji
/// (w EF Core: przy przechwyceniu DbUpdateConcurrencyException).
/// </summary>
public sealed class TodoListConcurrencyException() : Exception("Lista została zmieniona przez inny zapis.");
