using StudyPlanner.Domain.Notes;

namespace StudyPlanner.Application.Notes;

public interface INoteRepository
{
    Task<IReadOnlyList<Note>> GetByAuthorAsync(Guid authorId, CancellationToken cancellationToken);

    /// <summary>Zwraca notatkę tylko wtedy, gdy należy do podanego autora.</summary>
    Task<Note?> GetByIdAsync(Guid id, Guid authorId, CancellationToken cancellationToken);

    Task AddAsync(Note note, CancellationToken cancellationToken);

    /// <summary>
    /// Zapisuje zmiany notatki. Rzuca <see cref="NoteConcurrencyException"/>,
    /// gdy w międzyczasie ktoś zapisał nowszą wersję.
    /// </summary>
    Task UpdateAsync(Note note, CancellationToken cancellationToken);

    Task DeleteAsync(Note note, CancellationToken cancellationToken);
}

/// <summary>
/// Rzucany przez repozytorium przy konflikcie wersji
/// (w EF Core: przy przechwyceniu DbUpdateConcurrencyException).
/// </summary>
public sealed class NoteConcurrencyException() : Exception("Notatka została zmieniona przez inny zapis.");
