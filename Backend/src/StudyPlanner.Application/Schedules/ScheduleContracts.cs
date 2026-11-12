using StudyPlanner.Domain.Schedules;

namespace StudyPlanner.Application.Schedules;

/// <summary>
/// Daty zakresu w JSON mają postać "yyyy-MM-dd". <paramref name="Access"/> to dostęp bieżącego użytkownika
/// ("owner", "editor", "viewer") – frontend ukrywa po nim edycję w harmonogramach udostępnionych.
/// </summary>
public sealed record ScheduleDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version,
    ScheduleAccess Access)
{
    /// <summary>Bez <paramref name="access"/> – właściciel (tworzenie, edycja i konflikty dotyczą tylko jego).</summary>
    public static ScheduleDto From(Schedule schedule, ScheduleAccess access = ScheduleAccess.Owner) =>
        new(schedule.Id, schedule.Name, schedule.StartDate, schedule.EndDate, schedule.CreatedAt, schedule.UpdatedAt, schedule.Version, access);
}

/// <summary>Pola są opcjonalne, żeby brak wartości dało się zgłosić czytelnym komunikatem walidacji.</summary>
public sealed record CreateScheduleRequest(string? Name, DateOnly? StartDate, DateOnly? EndDate);

/// <summary><paramref name="Version"/> to wersja, którą klient ostatnio otrzymał z serwera.</summary>
public sealed record UpdateScheduleRequest(string? Name, DateOnly? StartDate, DateOnly? EndDate, int Version);
