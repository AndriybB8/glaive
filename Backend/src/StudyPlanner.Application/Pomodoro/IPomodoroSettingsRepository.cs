using StudyPlanner.Domain.Pomodoro;

namespace StudyPlanner.Application.Pomodoro;

public interface IPomodoroSettingsRepository
{
    /// <summary>Ustawienia użytkownika albo null, gdy jeszcze niczego nie zapisał (obowiązują domyślne).</summary>
    Task<PomodoroSettings?> GetByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Rzuca <see cref="PomodoroSettingsAlreadyExistsException"/>, gdy użytkownik ma już zapisane ustawienia.</summary>
    Task AddAsync(PomodoroSettings settings, CancellationToken cancellationToken);

    Task UpdateAsync(PomodoroSettings settings, CancellationToken cancellationToken);
}

/// <summary>
/// Rzucany przez repozytorium, gdy baza odrzuci dodanie ustawień, bo wiersz użytkownika już istnieje
/// (w EF Core: naruszenie klucza głównego). Zdarza się tylko przy pierwszym zapisie z dwóch kart naraz.
/// </summary>
public sealed class PomodoroSettingsAlreadyExistsException() : Exception("Ustawienia Pomodoro tego użytkownika już istnieją.");
