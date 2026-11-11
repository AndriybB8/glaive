namespace StudyPlanner.Application.TaskTags;

public enum TaskTagErrorType
{
    NotFound,
    Forbidden
}

public sealed record TaskTagError(TaskTagErrorType Type, string Message);

public sealed class TaskTagResult<T>
{
    private TaskTagResult(T? value, TaskTagError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public TaskTagError? Error { get; }

    public bool IsSuccess => Error is null;

    public static TaskTagResult<T> Success(T value) => new(value, null);

    public static TaskTagResult<T> Failure(TaskTagError error) => new(default, error);
}
