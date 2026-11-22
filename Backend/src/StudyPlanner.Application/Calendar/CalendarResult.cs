namespace StudyPlanner.Application.Calendar;

/// <summary>
/// Własne wydarzenia są prywatne, więc nie ma tu odmowy dostępu: cudze wydarzenie to brak wydarzenia (404).
/// Harmonogramy i zadania kalendarz tylko pokazuje – zmienia się je w ich własnym API.
/// </summary>
public enum CalendarErrorType
{
    NotFound,
    Validation,
    Conflict,
}

/// <param name="Current">Przy konflikcie wersji: aktualna wersja wydarzenia (albo zmienionego wystąpienia) z serwera.</param>
public sealed record CalendarError(CalendarErrorType Type, string Message, CalendarEventDto? Current = null);

public sealed class CalendarResult<T>
{
    private CalendarResult(T? value, CalendarError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public CalendarError? Error { get; }

    public bool IsSuccess => Error is null;

    public static CalendarResult<T> Success(T value) => new(value, null);

    public static CalendarResult<T> Failure(CalendarError error) => new(default, error);
}
