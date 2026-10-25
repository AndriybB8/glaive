using System.Globalization;
using StudyPlanner.Application.Common;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.Application.Tasks;

public sealed class TaskService(
    ITaskRepository tasks,
    IScheduleRepository schedules,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly TaskError TaskNotFound = new(TaskErrorType.NotFound, "Nie znaleziono zadania.");
    private static readonly TaskError ScheduleNotFound = new(TaskErrorType.NotFound, "Nie znaleziono harmonogramu.");

    /// <summary>Zadania harmonogramu: najpierw z najbliższym terminem (bez terminu na końcu), potem wg priorytetu.</summary>
    public async Task<TaskResult<IReadOnlyList<TaskDto>>> GetByScheduleAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        var schedule = await schedules.GetByIdAsync(scheduleId, currentUser.UserId, cancellationToken);
        if (schedule is null)
        {
            return TaskResult<IReadOnlyList<TaskDto>>.Failure(ScheduleNotFound);
        }

        var items = await tasks.GetByScheduleAsync(scheduleId, currentUser.UserId, cancellationToken);
        IReadOnlyList<TaskDto> result = items
            .OrderBy(t => t.DueDate is null)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .Select(TaskDto.From)
            .ToList();

        return TaskResult<IReadOnlyList<TaskDto>>.Success(result);
    }

    public async Task<TaskResult<TaskDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await FindAsync(id, cancellationToken);

        return found is null
            ? TaskResult<TaskDto>.Failure(TaskNotFound)
            : TaskResult<TaskDto>.Success(TaskDto.From(found.Value.Task));
    }

    public async Task<TaskResult<TaskDto>> CreateAsync(Guid scheduleId, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var schedule = await schedules.GetByIdAsync(scheduleId, currentUser.UserId, cancellationToken);
        if (schedule is null)
        {
            return TaskResult<TaskDto>.Failure(ScheduleNotFound);
        }

        var title = request.Title?.Trim() ?? string.Empty;
        var description = NormalizeDescription(request.Description);
        var priority = request.Priority ?? TaskPriority.Medium;
        var status = request.Status ?? TaskItemStatus.ToDo;

        if (Validate(title, description, request.DueDate, priority, status, schedule) is { } error)
        {
            return TaskResult<TaskDto>.Failure(error);
        }

        var task = TaskItem.Create(currentUser.UserId, schedule.Id, title, description, request.DueDate, priority, status, UtcNow());
        await tasks.AddAsync(task, cancellationToken);

        return TaskResult<TaskDto>.Success(TaskDto.From(task));
    }

    public async Task<TaskResult<TaskDto>> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var found = await FindAsync(id, cancellationToken);
        if (found is null)
        {
            return TaskResult<TaskDto>.Failure(TaskNotFound);
        }

        var (task, schedule) = found.Value;
        var title = request.Title?.Trim() ?? string.Empty;
        var description = NormalizeDescription(request.Description);
        var priority = request.Priority ?? task.Priority;
        var status = request.Status ?? task.Status;

        if (Validate(title, description, request.DueDate, priority, status, schedule) is { } error)
        {
            return TaskResult<TaskDto>.Failure(error);
        }

        if (task.Version != request.Version)
        {
            return TaskResult<TaskDto>.Failure(Conflict(task));
        }

        // Zapis bez zmian nie podbija wersji ani daty.
        if (task.Title == title
            && task.Description == description
            && task.DueDate == request.DueDate
            && task.Priority == priority
            && task.Status == status)
        {
            return TaskResult<TaskDto>.Success(TaskDto.From(task));
        }

        task.Update(title, description, request.DueDate, priority, status, UtcNow());
        return await SaveAsync(task, cancellationToken);
    }

    public async Task<TaskResult<TaskDto>> ChangeStatusAsync(Guid id, ChangeTaskStatusRequest request, CancellationToken cancellationToken)
    {
        if (request.Status is not { } status || !Enum.IsDefined(status))
        {
            return TaskResult<TaskDto>.Failure(Invalid("Podaj prawidłowy status zadania."));
        }

        var found = await FindAsync(id, cancellationToken);
        if (found is null)
        {
            return TaskResult<TaskDto>.Failure(TaskNotFound);
        }

        var task = found.Value.Task;
        if (task.Status == status)
        {
            return TaskResult<TaskDto>.Success(TaskDto.From(task));
        }

        task.ChangeStatus(status, UtcNow());
        return await SaveAsync(task, cancellationToken);
    }

    public async Task<TaskResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await FindAsync(id, cancellationToken);
        if (found is null)
        {
            return TaskResult<bool>.Failure(TaskNotFound);
        }

        await tasks.DeleteAsync(found.Value.Task, cancellationToken);
        return TaskResult<bool>.Success(true);
    }

    /// <summary>
    /// Zadanie użytkownika razem z jego harmonogramem. Zadania usuniętego harmonogramu traktujemy
    /// jak nieistniejące (w bazie usunie je kaskada).
    /// </summary>
    private async Task<(TaskItem Task, Schedule Schedule)?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        var schedule = await schedules.GetByIdAsync(task.ScheduleId, currentUser.UserId, cancellationToken);
        return schedule is null ? null : (task, schedule);
    }

    private async Task<TaskResult<TaskDto>> SaveAsync(TaskItem task, CancellationToken cancellationToken)
    {
        try
        {
            await tasks.UpdateAsync(task, cancellationToken);
        }
        catch (TaskConcurrencyException)
        {
            var current = await tasks.GetByIdAsync(task.Id, currentUser.UserId, cancellationToken);
            return current is null
                ? TaskResult<TaskDto>.Failure(TaskNotFound)
                : TaskResult<TaskDto>.Failure(Conflict(current));
        }

        return TaskResult<TaskDto>.Success(TaskDto.From(task));
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static TaskError Conflict(TaskItem current) =>
        new(TaskErrorType.Conflict, "Zadanie zostało w międzyczasie zmienione w innym miejscu.", TaskDto.From(current));

    /// <summary>Opis bez białych znaków na brzegach; pusty traktujemy jak brak opisu.</summary>
    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static TaskError? Validate(
        string title,
        string? description,
        DateOnly? dueDate,
        TaskPriority priority,
        TaskItemStatus status,
        Schedule schedule)
    {
        if (title.Length == 0)
        {
            return Invalid("Podaj tytuł zadania.");
        }

        if (title.Length > TaskItem.TitleMaxLength)
        {
            return Invalid($"Tytuł może mieć maksymalnie {TaskItem.TitleMaxLength} znaków.");
        }

        if (description?.Length > TaskItem.DescriptionMaxLength)
        {
            return Invalid($"Opis może mieć maksymalnie {TaskItem.DescriptionMaxLength} znaków.");
        }

        if (dueDate is { } due && (due < schedule.StartDate || due > schedule.EndDate))
        {
            return Invalid($"Termin musi mieścić się w harmonogramie ({FormatDate(schedule.StartDate)}–{FormatDate(schedule.EndDate)}).");
        }

        if (!Enum.IsDefined(priority))
        {
            return Invalid("Nieprawidłowy priorytet.");
        }

        if (!Enum.IsDefined(status))
        {
            return Invalid("Nieprawidłowy status.");
        }

        return null;
    }

    private static string FormatDate(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private static TaskError Invalid(string message) => new(TaskErrorType.Validation, message);
}
