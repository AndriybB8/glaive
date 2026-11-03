namespace StudyPlanner.Domain.Pomodoro;

/// <summary>
/// Ustawienia timera Pomodoro – jeden zestaw na użytkownika (kluczem jest <see cref="UserId"/>).
/// Dopóki użytkownik nic nie zapisze, ustawień nie ma w bazie i obowiązują wartości domyślne.
/// Czasy są w pełnych minutach. Wszystkie daty są w UTC.
/// </summary>
public class PomodoroSettings
{
    public const int DefaultWorkMinutes = 25;
    public const int DefaultShortBreakMinutes = 5;
    public const int DefaultLongBreakMinutes = 15;
    public const int DefaultSessionsBeforeLongBreak = 4;

    /// <summary>Najkrótszy czas każdej fazy (praca, krótka i długa przerwa).</summary>
    public const int MinMinutes = 1;
    public const int MaxWorkMinutes = 120;
    public const int MaxShortBreakMinutes = 30;
    public const int MaxLongBreakMinutes = 60;

    public const int MinSessionsBeforeLongBreak = 1;
    public const int MaxSessionsBeforeLongBreak = 10;

    /// <summary>Właściciel ustawień i zarazem klucz główny. W bazie: klucz obcy do tabeli users.</summary>
    public Guid UserId { get; private set; }

    public int WorkMinutes { get; private set; }

    public int ShortBreakMinutes { get; private set; }

    public int LongBreakMinutes { get; private set; }

    /// <summary>Po tylu sesjach pracy zamiast krótkiej jest długa przerwa.</summary>
    public int SessionsBeforeLongBreak { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    // Wymagany przez EF Core.
    private PomodoroSettings()
    {
    }

    public static PomodoroSettings Create(
        Guid userId,
        int workMinutes,
        int shortBreakMinutes,
        int longBreakMinutes,
        int sessionsBeforeLongBreak,
        DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(workMinutes, shortBreakMinutes, longBreakMinutes, sessionsBeforeLongBreak);

        return new PomodoroSettings
        {
            UserId = userId,
            WorkMinutes = workMinutes,
            ShortBreakMinutes = shortBreakMinutes,
            LongBreakMinutes = longBreakMinutes,
            SessionsBeforeLongBreak = sessionsBeforeLongBreak,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    public void Update(int workMinutes, int shortBreakMinutes, int longBreakMinutes, int sessionsBeforeLongBreak, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(workMinutes, shortBreakMinutes, longBreakMinutes, sessionsBeforeLongBreak);

        WorkMinutes = workMinutes;
        ShortBreakMinutes = shortBreakMinutes;
        LongBreakMinutes = longBreakMinutes;
        SessionsBeforeLongBreak = sessionsBeforeLongBreak;
        UpdatedAt = utcNow;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Data musi być w UTC.", nameof(value));
        }
    }

    private static void EnsureValid(int workMinutes, int shortBreakMinutes, int longBreakMinutes, int sessionsBeforeLongBreak)
    {
        if (workMinutes < MinMinutes || workMinutes > MaxWorkMinutes)
        {
            throw new ArgumentException($"Czas pracy musi wynosić od {MinMinutes} do {MaxWorkMinutes} minut.", nameof(workMinutes));
        }

        if (shortBreakMinutes < MinMinutes || shortBreakMinutes > MaxShortBreakMinutes)
        {
            throw new ArgumentException($"Krótka przerwa musi wynosić od {MinMinutes} do {MaxShortBreakMinutes} minut.", nameof(shortBreakMinutes));
        }

        if (longBreakMinutes < MinMinutes || longBreakMinutes > MaxLongBreakMinutes)
        {
            throw new ArgumentException($"Długa przerwa musi wynosić od {MinMinutes} do {MaxLongBreakMinutes} minut.", nameof(longBreakMinutes));
        }

        if (sessionsBeforeLongBreak < MinSessionsBeforeLongBreak || sessionsBeforeLongBreak > MaxSessionsBeforeLongBreak)
        {
            throw new ArgumentException(
                $"Liczba sesji przed długą przerwą musi wynosić od {MinSessionsBeforeLongBreak} do {MaxSessionsBeforeLongBreak}.",
                nameof(sessionsBeforeLongBreak));
        }
    }
}
