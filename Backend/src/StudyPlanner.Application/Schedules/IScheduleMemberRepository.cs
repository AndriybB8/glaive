using StudyPlanner.Domain.Schedules;

namespace StudyPlanner.Application.Schedules;

public interface IScheduleMemberRepository
{
    Task<IReadOnlyList<ScheduleMemberWithEmail>> GetByScheduleAsync(Guid scheduleId, CancellationToken cancellationToken);

    Task<ScheduleMemberWithEmail?> GetAsync(Guid scheduleId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduleInvitationDto>> GetPendingByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<AddScheduleMemberResult> AddAsync(ScheduleMember member, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(ScheduleMember member, CancellationToken cancellationToken);

    Task<bool> RemoveAsync(Guid scheduleId, Guid userId, CancellationToken cancellationToken);
}

public sealed record ScheduleMemberWithEmail(ScheduleMember Member, string Email);

public enum AddScheduleMemberResult
{
    Added,
    AlreadyExists,
    ReferenceMissing,
}
