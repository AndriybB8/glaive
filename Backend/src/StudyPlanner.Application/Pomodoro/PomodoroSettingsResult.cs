namespace StudyPlanner.Application.Pomodoro;

/// <summary>
/// Jedyny błąd przy ustawieniach to błąd walidacji (400). Nie ma NotFound – brak zapisu oznacza wartości domyślne –
/// ani Conflict – przy własnych ustawieniach wygrywa ostatni zapis.
/// </summary>
public enum PomodoroSettingsErrorType
{
    Validation,
}

public sealed record PomodoroSettingsError(PomodoroSettingsErrorType Type, string Message);

public sealed class PomodoroSettingsResult<T>
{
    private PomodoroSettingsResult(T? value, PomodoroSettingsError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public PomodoroSettingsError? Error { get; }

    public bool IsSuccess => Error is null;

    public static PomodoroSettingsResult<T> Success(T value) => new(value, null);

    public static PomodoroSettingsResult<T> Failure(PomodoroSettingsError error) => new(default, error);
}
