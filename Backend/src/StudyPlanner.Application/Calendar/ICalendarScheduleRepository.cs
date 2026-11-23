using StudyPlanner.Application.Common;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.Application.Calendar;

/// <summary>Zadanie z terminem razem z nazwą harmonogramu i dostępem użytkownika do niego.</summary>
public sealed record CalendarTask(TaskItem Task, string ScheduleName, AccessRole Access);

/// <summary>
/// Harmonogramy i zadania widoczne w kalendarzu – tylko do odczytu. Dostęp jak w harmonogramach: własne oraz wspólne
/// po przyjęciu zaproszenia (oczekujące zaproszenia nie dają dostępu). Osobny interfejs, żeby kalendarz nie zmieniał
/// repozytoriów harmonogramów i zadań.
/// </summary>
public interface ICalendarScheduleRepository
{
    /// <summary>Harmonogramy, których zakres dat ma wspólny choć jeden dzień z <paramref name="from"/>–<paramref name="to"/>.</summary>
    Task<IReadOnlyList<ScheduleWithAccess>> GetSchedulesAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Zadania z terminem w <paramref name="from"/>–<paramref name="to"/> (włącznie). Zadania bez terminu się nie pojawiają.</summary>
    Task<IReadOnlyList<CalendarTask>> GetTasksDueAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
