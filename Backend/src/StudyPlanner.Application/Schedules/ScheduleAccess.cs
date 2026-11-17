using StudyPlanner.Application.Common;
using StudyPlanner.Domain.Schedules;

namespace StudyPlanner.Application.Schedules;

/// <summary>Harmonogram razem z rolą, jaką ma w nim użytkownik.</summary>
public sealed record ScheduleWithAccess(Schedule Schedule, AccessRole Access);

public static class ScheduleAccessExtensions
{
    public static AccessRole ToAccess(this ScheduleMemberRole role) =>
        role == ScheduleMemberRole.Editor ? AccessRole.Editor : AccessRole.Viewer;
}
