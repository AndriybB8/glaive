using StudyPlanner.Domain.Schedules;

namespace StudyPlanner.Application.Schedules;

/// <summary>Daty zakresu w JSON mają postać "yyyy-MM-dd".</summary>
public sealed record ScheduleDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version)
{
    public static ScheduleDto From(Schedule schedule) =>
        new(schedule.Id, schedule.Name, schedule.StartDate, schedule.EndDate, schedule.CreatedAt, schedule.UpdatedAt, schedule.Version);
}

/// <summary>Pola są opcjonalne, żeby brak wartości dało się zgłosić czytelnym komunikatem walidacji.</summary>
public sealed record CreateScheduleRequest(string? Name, DateOnly? StartDate, DateOnly? EndDate);

/// <summary><paramref name="Version"/> to wersja, którą klient ostatnio otrzymał z serwera.</summary>
public sealed record UpdateScheduleRequest(string? Name, DateOnly? StartDate, DateOnly? EndDate, int Version);
