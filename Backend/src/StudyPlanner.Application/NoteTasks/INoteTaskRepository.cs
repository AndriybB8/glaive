using StudyPlanner.Application.Notes;
using StudyPlanner.Domain.NoteTasks;

namespace StudyPlanner.Application.NoteTasks;

/// <summary>
/// Dostęp do powiązań notatek z zadaniami. Wszystkie metody sprawdzają właściciela (userId),
/// więc nie da się podejrzeć ani połączyć cudzej notatki lub cudzego zadania.
/// </summary>
public interface INoteTaskRepository
{
    Task<bool> NoteExistsAsync(Guid noteId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Zadanie użytkownika z nazwą harmonogramu albo null, gdy nie istnieje (lub jego harmonogram usunięto).</summary>
    Task<LinkedTaskDto?> FindTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LinkedTaskDto>> GetTasksForNoteAsync(Guid noteId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Notatki przypisane do zadania – bez treści (podgląd pobiera ją osobno).</summary>
    Task<IReadOnlyList<NoteSummaryDto>> GetNotesForTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid noteId, Guid taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Zapisuje powiązanie. Jeśli już istnieje (np. dwa kliknięcia naraz), nic nie robi i zwraca true.
    /// Zwraca false, gdy notatka lub zadanie zostały w międzyczasie usunięte.
    /// </summary>
    Task<bool> AddAsync(NoteTaskLink link, CancellationToken cancellationToken);

    /// <summary>Usuwa powiązanie, jeśli istnieje.</summary>
    Task RemoveAsync(Guid noteId, Guid taskId, CancellationToken cancellationToken);
}
