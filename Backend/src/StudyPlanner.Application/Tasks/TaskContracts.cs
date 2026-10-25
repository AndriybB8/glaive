using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.Application.Tasks;

/// <summary>Termin w JSON ma postać "yyyy-MM-dd"; priorytet i status jako tekst ("high", "inProgress").</summary>
public sealed record TaskDto(
    Guid Id,
    Guid ScheduleId,
    string Title,
    string? Description,
    DateOnly? DueDate,
    TaskPriority Priority,
    TaskItemStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version)
{
    public static TaskDto From(TaskItem task) =>
        new(task.Id, task.ScheduleId, task.Title, task.Description, task.DueDate, task.Priority, task.Status, task.CreatedAt, task.UpdatedAt, task.Version);
}

/// <summary>Bez opisu i terminu – puste; bez priorytetu – średni, bez statusu – „do zrobienia”.</summary>
public sealed record CreateTaskRequest(string? Title, string? Description, DateOnly? DueDate, TaskPriority? Priority, TaskItemStatus? Status);

/// <summary>
/// <paramref name="Description"/> i <paramref name="DueDate"/> = null (lub pusty opis) usuwają wartość.
/// Brak priorytetu lub statusu zostawia obecną wartość.
/// <paramref name="Version"/> to wersja, którą klient ostatnio otrzymał z serwera.
/// </summary>
public sealed record UpdateTaskRequest(string? Title, string? Description, DateOnly? DueDate, TaskPriority? Priority, TaskItemStatus? Status, int Version);

/// <summary>Szybka zmiana statusu z listy – bez kontroli wersji (wygrywa ostatnia zmiana).</summary>
public sealed record ChangeTaskStatusRequest(TaskItemStatus? Status);
