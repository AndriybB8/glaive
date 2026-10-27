using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.Application.NoteTasks;

/// <summary>
/// Zadanie przypisane do notatki – tyle, ile potrzeba, żeby je pokazać przy notatce
/// (z nazwą harmonogramu, bo zadania z różnych harmonogramów mogą mieć ten sam tytuł).
/// </summary>
public sealed record LinkedTaskDto(
    Guid Id,
    Guid ScheduleId,
    string ScheduleName,
    string Title,
    TaskItemStatus Status,
    DateOnly? DueDate);
