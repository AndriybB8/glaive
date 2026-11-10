namespace StudyPlanner.Application.TodoLists;

public enum TodoItemErrorType
{
    NotFound,
    Validation,
    Conflict,
    Forbidden,
}

/// <param name="Current">Przy konflikcie: aktualna wersja punktu z serwera.</param>
public sealed record TodoItemError(TodoItemErrorType Type, string Message, TodoItemDto? Current = null);

public sealed class TodoItemResult<T>
{
    private TodoItemResult(T? value, TodoItemError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public TodoItemError? Error { get; }

    public bool IsSuccess => Error is null;

    public static TodoItemResult<T> Success(T value) => new(value, null);

    public static TodoItemResult<T> Failure(TodoItemError error) => new(default, error);
}
