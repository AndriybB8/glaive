using Microsoft.EntityFrameworkCore;
using StudyPlanner.Application.Calendar;
using StudyPlanner.Application.Common;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.Calendar;

/// <summary>Odczyt harmonogramów i zadań do kalendarza – dostęp liczony tak samo jak w EfScheduleRepository.</summary>
public sealed class EfCalendarScheduleRepository(AppDbContext db) : ICalendarScheduleRepository
{
    public async Task<IReadOnlyList<ScheduleWithAccess>> GetSchedulesAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var rows = await db.Schedules
            .AsNoTracking()
            .Where(s => s.StartDate <= to && s.EndDate >= from)
            .Where(s => s.UserId == userId
                || db.ScheduleMembers.Any(m => m.ScheduleId == s.Id && m.UserId == userId && m.AcceptedAt != null))
            .Select(s => new
            {
                Schedule = s,
                Role = db.ScheduleMembers
                    .Where(m => m.ScheduleId == s.Id && m.UserId == userId && m.AcceptedAt != null)
                    .Select(m => (ScheduleMemberRole?)m.Role)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => (row.Schedule, Access: ToAccess(row.Schedule.UserId, userId, row.Role)))
            .Where(row => row.Access is not null)
            .Select(row => new ScheduleWithAccess(row.Schedule, row.Access!.Value))
            .ToList();
    }

    public async Task<IReadOnlyList<CalendarTask>> GetTasksDueAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var rows = await db.Tasks
            .AsNoTracking()
            .Where(t => t.DueDate != null && t.DueDate >= from && t.DueDate <= to)
            .Join(db.Schedules, t => t.ScheduleId, s => s.Id, (t, s) => new { Task = t, Schedule = s })
            .Where(x => x.Schedule.UserId == userId
                || db.ScheduleMembers.Any(m => m.ScheduleId == x.Schedule.Id && m.UserId == userId && m.AcceptedAt != null))
            .Select(x => new
            {
                x.Task,
                ScheduleName = x.Schedule.Name,
                OwnerId = x.Schedule.UserId,
                Role = db.ScheduleMembers
                    .Where(m => m.ScheduleId == x.Schedule.Id && m.UserId == userId && m.AcceptedAt != null)
                    .Select(m => (ScheduleMemberRole?)m.Role)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => (row.Task, row.ScheduleName, Access: ToAccess(row.OwnerId, userId, row.Role)))
            .Where(row => row.Access is not null)
            .Select(row => new CalendarTask(row.Task, row.ScheduleName, row.Access!.Value))
            .ToList();
    }

    private static AccessRole? ToAccess(Guid ownerId, Guid userId, ScheduleMemberRole? role) =>
        ownerId == userId ? AccessRole.Owner : role?.ToAccess();
}
