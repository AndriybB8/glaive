namespace StudyPlanner.Application.Tasks;

public enum TaskErrorType
{
    NotFound,
    Validation,
    Conflict,
}

/// <param name="Current">Przy konflikcie: aktualna wersja zadania z serwera.</param>
public sealed record TaskError(TaskErrorType Type, string Message, TaskDto? Current = null);

public sealed class TaskResult<T>
{
    private TaskResult(T? value, TaskError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public TaskError? Error { get; }

    public bool IsSuccess => Error is null;

    public static TaskResult<T> Success(T value) => new(value, null);

    public static TaskResult<T> Failure(TaskError error) => new(default, error);
}
