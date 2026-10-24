namespace StudyPlanner.Application.Notes;

public enum NoteErrorType
{
    NotFound,
    Validation,
    Conflict,
}

/// <param name="Current">Przy konflikcie: aktualna wersja notatki z serwera.</param>
public sealed record NoteError(NoteErrorType Type, string Message, NoteDto? Current = null);

public sealed class NoteResult<T>
{
    private NoteResult(T? value, NoteError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public NoteError? Error { get; }

    public bool IsSuccess => Error is null;

    public static NoteResult<T> Success(T value) => new(value, null);

    public static NoteResult<T> Failure(NoteError error) => new(default, error);
}
