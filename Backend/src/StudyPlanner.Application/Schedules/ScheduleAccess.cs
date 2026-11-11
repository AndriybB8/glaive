using StudyPlanner.Domain.Schedules;

namespace StudyPlanner.Application.Schedules;

public enum ScheduleAccess
{
    Viewer,
    Editor,
    Owner,
}

public sealed record ScheduleWithAccess(Schedule Schedule, ScheduleAccess Access);

public static class ScheduleAccessExtensions
{
    public static bool CanEditTasks(this ScheduleAccess access) => access is ScheduleAccess.Editor or ScheduleAccess.Owner;

    public static bool CanManageSchedule(this ScheduleAccess access) => access is ScheduleAccess.Owner;

    public static ScheduleAccess ToAccess(this ScheduleMemberRole role) =>
        role == ScheduleMemberRole.Editor ? ScheduleAccess.Editor : ScheduleAccess.Viewer;
}
