using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.Application.Tasks;

public interface ITaskRepository
{
    Task<IReadOnlyList<TaskItem>> GetByScheduleAsync(Guid scheduleId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Zwraca zadanie tylko wtedy, gdy należy do podanego użytkownika.</summary>
    Task<TaskItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    Task AddAsync(TaskItem task, CancellationToken cancellationToken);

    /// <summary>
    /// Zapisuje zmiany zadania. Rzuca <see cref="TaskConcurrencyException"/>,
    /// gdy w międzyczasie ktoś zapisał nowszą wersję.
    /// </summary>
    Task UpdateAsync(TaskItem task, CancellationToken cancellationToken);

    Task DeleteAsync(TaskItem task, CancellationToken cancellationToken);
}

/// <summary>
/// Rzucany przez repozytorium przy konflikcie wersji
/// (w EF Core: przy przechwyceniu DbUpdateConcurrencyException).
/// </summary>
public sealed class TaskConcurrencyException() : Exception("Zadanie zostało zmienione przez inny zapis.");
