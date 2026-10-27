namespace StudyPlanner.Application.NoteTasks;

public enum NoteTaskErrorType
{
    NotFound,
}

public sealed record NoteTaskError(NoteTaskErrorType Type, string Message);

public sealed class NoteTaskResult<T>
{
    private NoteTaskResult(T? value, NoteTaskError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public NoteTaskError? Error { get; }

    public bool IsSuccess => Error is null;

    public static NoteTaskResult<T> Success(T value) => new(value, null);

    public static NoteTaskResult<T> Failure(NoteTaskError error) => new(default, error);
}
