namespace StudyPlanner.Application.NoteGroups;

public enum NoteGroupErrorType
{
    NotFound,
    Validation,
    Conflict,
}

/// <param name="Current">Przy konflikcie: aktualna wersja grupy z serwera.</param>
public sealed record NoteGroupError(NoteGroupErrorType Type, string Message, NoteGroupDto? Current = null);

public sealed class NoteGroupResult<T>
{
    private NoteGroupResult(T? value, NoteGroupError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public NoteGroupError? Error { get; }

    public bool IsSuccess => Error is null;

    public static NoteGroupResult<T> Success(T value) => new(value, null);

    public static NoteGroupResult<T> Failure(NoteGroupError error) => new(default, error);
}
