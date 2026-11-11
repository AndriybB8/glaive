using StudyPlanner.Domain.Schedules;

namespace StudyPlanner.Application.Schedules;

public sealed record ScheduleMemberDto(Guid UserId, string Email, ScheduleMemberRole Role, DateTime CreatedAt, DateTime? AcceptedAt)
{
    public static ScheduleMemberDto From(ScheduleMemberWithEmail found) =>
        new(found.Member.UserId, found.Email, found.Member.Role, found.Member.CreatedAt, found.Member.AcceptedAt);
}

public sealed record ScheduleInvitationDto(Guid ScheduleId, string ScheduleName, string OwnerEmail, ScheduleMemberRole Role, DateTime InvitedAt);

public sealed record InviteScheduleMemberRequest(string? Email, ScheduleMemberRole? Role);

public sealed record ChangeScheduleMemberRoleRequest(ScheduleMemberRole? Role);
