namespace StudyPlanner.Application.Tags;

public enum TagErrorType
{
    NotFound,
    Validation,
    Conflict,
}

public sealed record TagError(TagErrorType Type, string Message, TagDto? Current = null);

public sealed class TagResult<T>
{
    private TagResult(T? value, TagError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public TagError? Error { get; }

    public bool IsSuccess => Error is null;

    public static TagResult<T> Success(T value) => new(value, null);

    public static TagResult<T> Failure(TagError error) => new(default, error);
}
