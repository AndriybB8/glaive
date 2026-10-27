using StudyPlanner.Application.Common;
using StudyPlanner.Application.Notes;
using StudyPlanner.Domain.NoteTasks;

namespace StudyPlanner.Application.NoteTasks;

/// <summary>
/// Przypisywanie notatek do zadań. Powiązanie nie zmienia wersji notatki ani zadania,
/// więc nie przerywa autozapisu otwartej notatki ani edycji zadania.
/// </summary>
public sealed class NoteTaskService(INoteTaskRepository links, ICurrentUser currentUser, TimeProvider timeProvider)
{
    private static readonly NoteTaskError NoteNotFound = new(NoteTaskErrorType.NotFound, "Nie znaleziono notatki.");
    private static readonly NoteTaskError TaskNotFound = new(NoteTaskErrorType.NotFound, "Nie znaleziono zadania. Mogło zostać usunięte.");

    /// <summary>Zadania notatki, pogrupowane wg harmonogramu, w nim od najbliższego terminu.</summary>
    public async Task<NoteTaskResult<IReadOnlyList<LinkedTaskDto>>> GetTasksForNoteAsync(Guid noteId, CancellationToken cancellationToken)
    {
        if (!await links.NoteExistsAsync(noteId, currentUser.UserId, cancellationToken))
        {
            return NoteTaskResult<IReadOnlyList<LinkedTaskDto>>.Failure(NoteNotFound);
        }

        var tasks = await links.GetTasksForNoteAsync(noteId, currentUser.UserId, cancellationToken);
        IReadOnlyList<LinkedTaskDto> sorted = tasks
            .OrderBy(t => t.ScheduleName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(t => t.DueDate is null)
            .ThenBy(t => t.DueDate)
            .ThenBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return NoteTaskResult<IReadOnlyList<LinkedTaskDto>>.Success(sorted);
    }

    /// <summary>Notatki zadania, od ostatnio zmienionej.</summary>
    public async Task<NoteTaskResult<IReadOnlyList<NoteSummaryDto>>> GetNotesForTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        if (await links.FindTaskAsync(taskId, currentUser.UserId, cancellationToken) is null)
        {
            return NoteTaskResult<IReadOnlyList<NoteSummaryDto>>.Failure(TaskNotFound);
        }

        var notes = await links.GetNotesForTaskAsync(taskId, currentUser.UserId, cancellationToken);
        IReadOnlyList<NoteSummaryDto> sorted = notes.OrderByDescending(n => n.UpdatedAt).ToList();

        return NoteTaskResult<IReadOnlyList<NoteSummaryDto>>.Success(sorted);
    }

    /// <summary>Przypisuje notatkę do zadania. Ponowne przypisanie tej samej pary niczego nie psuje.</summary>
    public async Task<NoteTaskResult<LinkedTaskDto>> LinkAsync(Guid noteId, Guid taskId, CancellationToken cancellationToken)
    {
        if (!await links.NoteExistsAsync(noteId, currentUser.UserId, cancellationToken))
        {
            return NoteTaskResult<LinkedTaskDto>.Failure(NoteNotFound);
        }

        var task = await links.FindTaskAsync(taskId, currentUser.UserId, cancellationToken);
        if (task is null)
        {
            return NoteTaskResult<LinkedTaskDto>.Failure(TaskNotFound);
        }

        if (await links.ExistsAsync(noteId, taskId, cancellationToken))
        {
            return NoteTaskResult<LinkedTaskDto>.Success(task);
        }

        var link = NoteTaskLink.Create(noteId, taskId, timeProvider.GetUtcNow().UtcDateTime);
        if (!await links.AddAsync(link, cancellationToken))
        {
            return NoteTaskResult<LinkedTaskDto>.Failure(TaskNotFound);
        }

        return NoteTaskResult<LinkedTaskDto>.Success(task);
    }

    /// <summary>
    /// Odłącza notatkę od zadania. Brak powiązania (albo już usunięte zadanie) to też sukces –
    /// efekt jest ten sam, a druga karta nie dostanie zbędnego błędu.
    /// </summary>
    public async Task<NoteTaskResult<bool>> UnlinkAsync(Guid noteId, Guid taskId, CancellationToken cancellationToken)
    {
        if (!await links.NoteExistsAsync(noteId, currentUser.UserId, cancellationToken))
        {
            return NoteTaskResult<bool>.Failure(NoteNotFound);
        }

        await links.RemoveAsync(noteId, taskId, cancellationToken);
        return NoteTaskResult<bool>.Success(true);
    }
}
