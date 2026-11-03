namespace StudyPlanner.Application.TodoLists;

public enum TodoListErrorType
{
    NotFound,
    Validation,
    Conflict,
}

/// <param name="Current">Przy konflikcie: aktualna wersja listy z serwera.</param>
public sealed record TodoListError(TodoListErrorType Type, string Message, TodoListDto? Current = null);

public sealed class TodoListResult<T>
{
    private TodoListResult(T? value, TodoListError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public TodoListError? Error { get; }

    public bool IsSuccess => Error is null;

    public static TodoListResult<T> Success(T value) => new(value, null);

    public static TodoListResult<T> Failure(TodoListError error) => new(default, error);
}
