using StudyPlanner.Domain.Schedules;

namespace StudyPlanner.Application.Schedules;

public interface IScheduleRepository
{
    /// <summary>
    /// Harmonogramy użytkownika oraz te, do których przyjął zaproszenie – razem z jego dostępem.
    /// Oczekujące zaproszenia nie dają dostępu, więc ich harmonogramów tu nie ma.
    /// </summary>
    Task<IReadOnlyList<ScheduleWithAccess>> GetAccessibleByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Zwraca harmonogram tylko wtedy, gdy należy do podanego użytkownika.</summary>
    Task<Schedule?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    Task AddAsync(Schedule schedule, CancellationToken cancellationToken);

    /// <summary>
    /// Zapisuje zmiany harmonogramu. Rzuca <see cref="ScheduleConcurrencyException"/>,
    /// gdy w międzyczasie ktoś zapisał nowszą wersję.
    /// </summary>
    Task UpdateAsync(Schedule schedule, CancellationToken cancellationToken);

    Task DeleteAsync(Schedule schedule, CancellationToken cancellationToken);

    Task<ScheduleWithAccess?> GetWithAccessAsync(Guid id, Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Rzucany przez repozytorium przy konflikcie wersji
/// (w EF Core: przy przechwyceniu DbUpdateConcurrencyException).
/// </summary>
public sealed class ScheduleConcurrencyException() : Exception("Harmonogram został zmieniony przez inny zapis.");
