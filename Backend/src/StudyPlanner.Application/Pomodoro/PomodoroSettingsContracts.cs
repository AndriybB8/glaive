using StudyPlanner.Domain.Pomodoro;

namespace StudyPlanner.Application.Pomodoro;

/// <summary>
/// Nazwy pól są takie same jak w typie <c>PomodoroSettings</c> na frontendzie.
/// <paramref name="IsDefault"/> = użytkownik niczego jeszcze nie zapisał i dostaje wartości domyślne.
/// </summary>
public sealed record PomodoroSettingsDto(
    int WorkMinutes,
    int ShortBreakMinutes,
    int LongBreakMinutes,
    int SessionsBeforeLongBreak,
    bool IsDefault)
{
    public static readonly PomodoroSettingsDto Default = new(
        PomodoroSettings.DefaultWorkMinutes,
        PomodoroSettings.DefaultShortBreakMinutes,
        PomodoroSettings.DefaultLongBreakMinutes,
        PomodoroSettings.DefaultSessionsBeforeLongBreak,
        IsDefault: true);

    public static PomodoroSettingsDto From(PomodoroSettings settings) =>
        new(settings.WorkMinutes, settings.ShortBreakMinutes, settings.LongBreakMinutes, settings.SessionsBeforeLongBreak, IsDefault: false);
}

/// <summary>
/// Pełny zestaw ustawień (PUT zastępuje całość). Pola są opcjonalne, żeby brak wartości
/// dało się zgłosić czytelnym komunikatem walidacji.
/// </summary>
public sealed record UpdatePomodoroSettingsRequest(
    int? WorkMinutes,
    int? ShortBreakMinutes,
    int? LongBreakMinutes,
    int? SessionsBeforeLongBreak);
