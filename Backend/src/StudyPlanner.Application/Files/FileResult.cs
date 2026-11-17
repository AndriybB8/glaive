namespace StudyPlanner.Application.Files;

public enum FileErrorType
{
    NotFound,
    Validation,
    Conflict,
}

public sealed record FileError(FileErrorType Type, string Message, FileDto? Current = null);

public sealed class FileResult<T>
{
    private FileResult(T? value, FileError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public FileError? Error { get; }

    public bool IsSuccess => Error is null;

    public static FileResult<T> Success(T value) => new(value, null);

    public static FileResult<T> Failure(FileError error) => new(default, error);
}
