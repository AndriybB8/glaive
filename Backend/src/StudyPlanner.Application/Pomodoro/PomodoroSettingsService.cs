using StudyPlanner.Application.Common;
using StudyPlanner.Domain.Pomodoro;

namespace StudyPlanner.Application.Pomodoro;

public sealed class PomodoroSettingsService(IPomodoroSettingsRepository repository, ICurrentUser currentUser, TimeProvider timeProvider)
{
    /// <summary>Zapisane ustawienia użytkownika, a gdy jeszcze ich nie ma – wartości domyślne (bez zapisu do bazy).</summary>
    public async Task<PomodoroSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await repository.GetByUserAsync(currentUser.UserId, cancellationToken);

        return settings is null ? PomodoroSettingsDto.Default : PomodoroSettingsDto.From(settings);
    }

    /// <summary>Zapisuje ustawienia: przy pierwszym zapisie je tworzy, potem nadpisuje.</summary>
    public async Task<PomodoroSettingsResult<PomodoroSettingsDto>> UpdateAsync(
        UpdatePomodoroSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (Validate(request) is { } error)
        {
            return PomodoroSettingsResult<PomodoroSettingsDto>.Failure(error);
        }

        var workMinutes = request.WorkMinutes!.Value;
        var shortBreakMinutes = request.ShortBreakMinutes!.Value;
        var longBreakMinutes = request.LongBreakMinutes!.Value;
        var sessionsBeforeLongBreak = request.SessionsBeforeLongBreak!.Value;

        var settings = await repository.GetByUserAsync(currentUser.UserId, cancellationToken);

        if (settings is null)
        {
            var created = PomodoroSettings.Create(
                currentUser.UserId, workMinutes, shortBreakMinutes, longBreakMinutes, sessionsBeforeLongBreak, UtcNow());

            try
            {
                await repository.AddAsync(created, cancellationToken);
                return PomodoroSettingsResult<PomodoroSettingsDto>.Success(PomodoroSettingsDto.From(created));
            }
            catch (PomodoroSettingsAlreadyExistsException)
            {
                // Pierwszy zapis z dwóch kart naraz – ten zapis nadpisuje ustawienia utworzone przed chwilą przez drugą kartę.
                settings = await repository.GetByUserAsync(currentUser.UserId, cancellationToken)
                    ?? throw new InvalidOperationException("Ustawienia Pomodoro zniknęły zaraz po utworzeniu.");
            }
        }

        // Zapis bez zmian (np. „Zapisz” bez edycji) nie zmienia daty modyfikacji.
        if (settings.WorkMinutes == workMinutes
            && settings.ShortBreakMinutes == shortBreakMinutes
            && settings.LongBreakMinutes == longBreakMinutes
            && settings.SessionsBeforeLongBreak == sessionsBeforeLongBreak)
        {
            return PomodoroSettingsResult<PomodoroSettingsDto>.Success(PomodoroSettingsDto.From(settings));
        }

        settings.Update(workMinutes, shortBreakMinutes, longBreakMinutes, sessionsBeforeLongBreak, UtcNow());
        await repository.UpdateAsync(settings, cancellationToken);

        return PomodoroSettingsResult<PomodoroSettingsDto>.Success(PomodoroSettingsDto.From(settings));
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static PomodoroSettingsError? Validate(UpdatePomodoroSettingsRequest request)
    {
        const int min = PomodoroSettings.MinMinutes;

        if (request.WorkMinutes is not { } work)
        {
            return Invalid("Podaj czas pracy.");
        }

        if (work is < min or > PomodoroSettings.MaxWorkMinutes)
        {
            return Invalid($"Czas pracy musi wynosić od {min} do {PomodoroSettings.MaxWorkMinutes} minut.");
        }

        if (request.ShortBreakMinutes is not { } shortBreak)
        {
            return Invalid("Podaj czas krótkiej przerwy.");
        }

        if (shortBreak is < min or > PomodoroSettings.MaxShortBreakMinutes)
        {
            return Invalid($"Krótka przerwa musi wynosić od {min} do {PomodoroSettings.MaxShortBreakMinutes} minut.");
        }

        if (request.LongBreakMinutes is not { } longBreak)
        {
            return Invalid("Podaj czas długiej przerwy.");
        }

        if (longBreak is < min or > PomodoroSettings.MaxLongBreakMinutes)
        {
            return Invalid($"Długa przerwa musi wynosić od {min} do {PomodoroSettings.MaxLongBreakMinutes} minut.");
        }

        if (request.SessionsBeforeLongBreak is not { } sessions)
        {
            return Invalid("Podaj liczbę sesji przed długą przerwą.");
        }

        if (sessions is < PomodoroSettings.MinSessionsBeforeLongBreak or > PomodoroSettings.MaxSessionsBeforeLongBreak)
        {
            return Invalid(
                $"Liczba sesji przed długą przerwą musi wynosić od {PomodoroSettings.MinSessionsBeforeLongBreak} " +
                $"do {PomodoroSettings.MaxSessionsBeforeLongBreak}.");
        }

        return null;
    }

    private static PomodoroSettingsError Invalid(string message) => new(PomodoroSettingsErrorType.Validation, message);
}
