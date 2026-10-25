namespace StudyPlanner.Application.Schedules;

public enum ScheduleErrorType
{
    NotFound,
    Validation,
    Conflict,
}

/// <param name="Current">Przy konflikcie: aktualna wersja harmonogramu z serwera.</param>
public sealed record ScheduleError(ScheduleErrorType Type, string Message, ScheduleDto? Current = null);

public sealed class ScheduleResult<T>
{
    private ScheduleResult(T? value, ScheduleError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public ScheduleError? Error { get; }

    public bool IsSuccess => Error is null;

    public static ScheduleResult<T> Success(T value) => new(value, null);

    public static ScheduleResult<T> Failure(ScheduleError error) => new(default, error);
}
