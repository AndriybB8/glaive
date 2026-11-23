using StudyPlanner.Application.Common;
using StudyPlanner.Domain.Calendar;
using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.Application.Calendar;

/// <summary>
/// Wydarzenie jednorazowe albo seria. Wydarzenie w godzinach ma <paramref name="StartsAt"/>/<paramref name="EndsAt"/> (UTC),
/// całodniowe – <paramref name="StartDate"/>/<paramref name="EndDate"/> ("yyyy-MM-dd", koniec włącznie).
/// Przy konflikcie może to być też zmienione wystąpienie serii – wtedy ma <paramref name="SeriesId"/> i <paramref name="OriginalDate"/>.
/// </summary>
public sealed record CalendarEventDto(
    Guid Id,
    string Title,
    string? Description,
    string Color,
    bool IsAllDay,
    DateTime? StartsAt,
    DateTime? EndsAt,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string TimeZone,
    RecurrenceFrequency Recurrence,
    DateOnly? RecurrenceUntil,
    Guid? SeriesId,
    DateOnly? OriginalDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version)
{
    public static CalendarEventDto From(CalendarEvent calendarEvent) =>
        new(
            calendarEvent.Id,
            calendarEvent.Title,
            calendarEvent.Description,
            calendarEvent.Color,
            calendarEvent.IsAllDay,
            calendarEvent.StartsAt,
            calendarEvent.EndsAt,
            calendarEvent.StartDate,
            calendarEvent.EndDate,
            calendarEvent.TimeZone,
            calendarEvent.Recurrence,
            calendarEvent.RecurrenceUntil,
            calendarEvent.SeriesId,
            calendarEvent.OriginalDate,
            calendarEvent.CreatedAt,
            calendarEvent.UpdatedAt,
            calendarEvent.Version);
}

/// <summary>
/// Pola są opcjonalne, żeby brak wartości dało się zgłosić czytelnym komunikatem walidacji.
/// Gdy <paramref name="IsAllDay"/> = true, liczą się <paramref name="StartDate"/>/<paramref name="EndDate"/> (brak końca = jeden dzień),
/// w przeciwnym razie <paramref name="StartsAt"/>/<paramref name="EndsAt"/> w UTC (np. "2026-11-23T09:00:00Z").
/// <paramref name="TimeZone"/> to strefa IANA użytkownika (we frontendzie: Intl.DateTimeFormat().resolvedOptions().timeZone).
/// Brak koloru = <see cref="CalendarEvent.DefaultColor"/>, brak powtarzania = "none".
/// </summary>
public sealed record CreateCalendarEventRequest(
    string? Title,
    string? Description,
    string? Color,
    bool? IsAllDay,
    DateTime? StartsAt,
    DateTime? EndsAt,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? TimeZone,
    RecurrenceFrequency? Recurrence,
    DateOnly? RecurrenceUntil);

/// <summary>Które wystąpienia serii zmienić albo usunąć – jak w Google Calendar.</summary>
public enum CalendarEditScope
{
    /// <summary>Tylko to wystąpienie („To wydarzenie”).</summary>
    Single,

    /// <summary>To i wszystkie następne („To i następne wydarzenia”).</summary>
    Following,

    /// <summary>Cała seria („Wszystkie wydarzenia”).</summary>
    All,
}

/// <summary>
/// Pola jak przy tworzeniu. <paramref name="Version"/> to wersja, którą klient ostatnio otrzymał:
/// dla wydarzenia jednorazowego i dla "single" – <see cref="CalendarItemDto.Version"/> klikniętego elementu
/// (0 dla wystąpienia bez osobnych zmian),
/// dla "following" i "all" – <see cref="CalendarItemDto.SeriesVersion"/> (albo <see cref="CalendarEventDto.Version"/> serii).
/// Dla serii trzeba podać <paramref name="Scope"/>, a dla "single" i "following" także <paramref name="OccurrenceDate"/>
/// (<see cref="CalendarItemDto.OccurrenceDate"/>). Przy "all" z <paramref name="OccurrenceDate"/> czas z żądania dotyczy tego
/// wystąpienia, a seria przesuwa się o tyle samo; bez niego czas z żądania to czas pierwszego wystąpienia serii.
/// </summary>
public sealed record UpdateCalendarEventRequest(
    string? Title,
    string? Description,
    string? Color,
    bool? IsAllDay,
    DateTime? StartsAt,
    DateTime? EndsAt,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? TimeZone,
    RecurrenceFrequency? Recurrence,
    DateOnly? RecurrenceUntil,
    int Version,
    CalendarEditScope? Scope,
    DateOnly? OccurrenceDate);

/// <summary>Rodzaj elementu w kalendarzu. W JSON: "event", "schedule", "task".</summary>
public enum CalendarItemKind
{
    /// <summary>Własne wydarzenie (albo jedno wystąpienie serii).</summary>
    Event,

    /// <summary>Harmonogram – całodniowy pasek od pierwszego do ostatniego dnia.</summary>
    Schedule,

    /// <summary>Zadanie z terminem – całodniowe w dniu terminu.</summary>
    Task,
}

/// <summary>
/// Jeden element w widoku kalendarza. Wspólne pola opisują, gdzie go narysować; pozostałe zależą od rodzaju.
/// </summary>
/// <param name="Key">Unikalny w odpowiedzi – np. "event:{id}:2026-11-23" dla wystąpienia serii, "task:{id}".</param>
/// <param name="Id">Id wydarzenia (dla wystąpienia – id serii), harmonogramu albo zadania.</param>
/// <param name="StartsAt">Wydarzenie w godzinach: początek w UTC. Pozostałe elementy są całodniowe.</param>
/// <param name="StartDate">Element całodniowy: pierwszy dzień ("yyyy-MM-dd"); <paramref name="EndDate"/> to ostatni dzień (włącznie).</param>
/// <param name="Color">Kolor wydarzenia (#RRGGBB). Harmonogramy i zadania nie mają koloru – ustala go frontend.</param>
/// <param name="Access">Dostęp bieżącego użytkownika: dla własnych wydarzeń "owner", dla harmonogramów i zadań – rola w harmonogramie.</param>
/// <param name="OccurrenceDate">Wystąpienie serii: jego dzień według serii – potrzebny do zmiany albo usunięcia tylko tego wystąpienia.</param>
/// <param name="IsException">Wystąpienie serii zmienione osobno („tylko to wydarzenie”).</param>
/// <param name="Version">Wydarzenie: wersja do zmiany samego elementu – wydarzenia jednorazowego albo wystąpienia ("single").
/// Wystąpienie bez osobnych zmian ma 0, zmienione wystąpienie – własną wersję (od 1).</param>
/// <param name="SeriesVersion">Wystąpienie serii: wersja serii do zmian "following" i "all".</param>
public sealed record CalendarItemDto(
    string Key,
    CalendarItemKind Kind,
    Guid Id,
    string Title,
    string? Description,
    bool IsAllDay,
    DateTime? StartsAt,
    DateTime? EndsAt,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Color,
    AccessRole Access,
    DateOnly? OccurrenceDate,
    bool IsRecurring,
    bool IsException,
    int? Version,
    int? SeriesVersion,
    Guid? ScheduleId,
    string? ScheduleName,
    TaskItemStatus? TaskStatus,
    TaskPriority? TaskPriority);
