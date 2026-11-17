using StudyPlanner.Application.Common;
using StudyPlanner.Application.Users;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Application.Schedules;

public sealed class ScheduleMemberService(
    IScheduleMemberRepository members,
    IScheduleRepository schedules,
    IUserRepository users,
    IScheduleNotifier notifier,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly ScheduleError ScheduleNotFound = new(ScheduleErrorType.NotFound, "Nie znaleziono harmonogramu.");

    private static readonly ScheduleError UserNotFound = new(ScheduleErrorType.NotFound, "Nie znaleziono użytkownika o tym adresie.");
    private static readonly ScheduleError MemberNotFound = new(ScheduleErrorType.NotFound, "Ta osoba nie ma dostępu do harmonogramu.");
    private static readonly ScheduleError InvitationNotFound = new(ScheduleErrorType.NotFound, "Nie znaleziono zaproszenia.");
    private static readonly ScheduleError Forbidden = new(ScheduleErrorType.Forbidden, "Tylko właściciel może zarządzać dostępem do harmonogramu.");
    private static readonly ScheduleError AlreadyMember = new(ScheduleErrorType.Conflict, "Ta osoba ma już dostęp do harmonogramu albo zaproszenie.");

    public async Task<ScheduleResult<IReadOnlyList<ScheduleMemberDto>>> GetByScheduleAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        var (_, denied) = await FindOwnedAsync(scheduleId, cancellationToken);
        if (denied is not null)
            return ScheduleResult<IReadOnlyList<ScheduleMemberDto>>.Failure(denied);

        var found = await members.GetByScheduleAsync(scheduleId, cancellationToken);
        IReadOnlyList<ScheduleMemberDto> result = found
            .OrderBy(m => m.Member.CreatedAt)
            .Select(ScheduleMemberDto.From)
            .ToList();

        return ScheduleResult<IReadOnlyList<ScheduleMemberDto>>.Success(result);
    }

    public async Task<ScheduleResult<ScheduleMemberDto>> InviteAsync(Guid scheduleId, InviteScheduleMemberRequest request, CancellationToken cancellationToken)
    {
        var (schedule, denied) = await FindOwnedAsync(scheduleId, cancellationToken);
        if (denied is not null)
            return ScheduleResult<ScheduleMemberDto>.Failure(denied);

        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (email.Length == 0)
        {
            return ScheduleResult<ScheduleMemberDto>.Failure(Invalid("Podaj adres email."));
        }

        if (ValidateRole(request.Role) is { } invalidRole)
        {
            return ScheduleResult<ScheduleMemberDto>.Failure(invalidRole);
        }

        var user = await users.GetByEmailAsync(email, cancellationToken);
        if (user is null || user.Status != AccountStatus.Active)
        {
            return ScheduleResult<ScheduleMemberDto>.Failure(UserNotFound);
        }

        if (user.Id == currentUser.UserId)
        {
            return ScheduleResult<ScheduleMemberDto>.Failure(Invalid("Nie możesz zaprosić samego siebie."));
        }

        var member = ScheduleMember.Invite(scheduleId, user.Id, request.Role!.Value, UtcNow());

        switch (await members.AddAsync(member, cancellationToken))
        {
            case AddScheduleMemberResult.Added:
                await notifier.InvitationReceivedAsync(user.Id, scheduleId, schedule!.Name);
                return ScheduleResult<ScheduleMemberDto>.Success(ScheduleMemberDto.From(new ScheduleMemberWithEmail(member, user.Email)));
            case AddScheduleMemberResult.AlreadyExists:
                return ScheduleResult<ScheduleMemberDto>.Failure(AlreadyMember);
            default:
                return ScheduleResult<ScheduleMemberDto>.Failure(ScheduleNotFound);
        }
    }

    public async Task<ScheduleResult<ScheduleMemberDto>> ChangeRoleAsync(
        Guid scheduleId,
        Guid userId,
        ChangeScheduleMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var (schedule, denied) = await FindOwnedAsync(scheduleId, cancellationToken);
        if (denied is not null)
            return ScheduleResult<ScheduleMemberDto>.Failure(denied);

        if (ValidateRole(request.Role) is { } invalidRole)
            return ScheduleResult<ScheduleMemberDto>.Failure(invalidRole);

        var found = await members.GetAsync(scheduleId, userId, cancellationToken);
        if (found is null)
            return ScheduleResult<ScheduleMemberDto>.Failure(MemberNotFound);

        var role = request.Role!.Value;
        if (found.Member.Role != role)
        {
            found.Member.ChangeRole(role);

            if (!await members.UpdateAsync(found.Member, cancellationToken))
                return ScheduleResult<ScheduleMemberDto>.Failure(MemberNotFound);

            await NotifyMemberChangeAsync(ScheduleChangeKind.MemberRoleChanged, schedule!, userId);
        }

        return ScheduleResult<ScheduleMemberDto>.Success(ScheduleMemberDto.From(found));
    }

    public async Task<ScheduleResult<bool>> RemoveAsync(Guid scheduleId, Guid userId, CancellationToken cancellationToken)
    {
        var schedule = await schedules.GetWithAccessAsync(scheduleId, currentUser.UserId, cancellationToken);
        if (schedule is null)
            return ScheduleResult<bool>.Failure(ScheduleNotFound);

        if (!schedule.Access.Can(Permission.Manage) && userId != currentUser.UserId)
            return ScheduleResult<bool>.Failure(Forbidden);

        if (!await members.RemoveAsync(scheduleId, userId, cancellationToken))
            return ScheduleResult<bool>.Failure(MemberNotFound);

        await NotifyMemberChangeAsync(ScheduleChangeKind.MemberRemoved, schedule.Schedule, userId);
        return ScheduleResult<bool>.Success(true);
    }

    public async Task<IReadOnlyList<ScheduleInvitationDto>> GetInvitationsAsync(CancellationToken cancellationToken)
    {
        var found = await members.GetPendingByUserAsync(currentUser.UserId, cancellationToken);
        return found.OrderByDescending(i => i.InvitedAt).ToList();
    }

    public async Task<ScheduleResult<bool>> AcceptInvitationAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        var found = await members.GetAsync(scheduleId, currentUser.UserId, cancellationToken);
        if (found is null)
            return ScheduleResult<bool>.Failure(InvitationNotFound);

        if (found.Member.AcceptedAt != null)
            return ScheduleResult<bool>.Success(true);

        found.Member.Accept(UtcNow());

        if (!await members.UpdateAsync(found.Member, cancellationToken))
            return ScheduleResult<bool>.Failure(InvitationNotFound);

        var schedule = await schedules.GetWithAccessAsync(scheduleId, currentUser.UserId, cancellationToken);
        if (schedule is not null)
            await NotifyMemberChangeAsync(ScheduleChangeKind.MemberJoined, schedule.Schedule, currentUser.UserId);

        return ScheduleResult<bool>.Success(true);
    }

    public async Task<ScheduleResult<bool>> DeclineInvitationAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        var found = await members.GetAsync(scheduleId, currentUser.UserId, cancellationToken);
        if (found is null || found.Member.AcceptedAt != null)
            return ScheduleResult<bool>.Failure(InvitationNotFound);

        return await members.RemoveAsync(scheduleId, currentUser.UserId, cancellationToken)
            ? ScheduleResult<bool>.Success(true)
            : ScheduleResult<bool>.Failure(InvitationNotFound);
    }

    /// <summary>Harmonogram, jeśli bieżący użytkownik jest jego właścicielem; w przeciwnym razie błąd.</summary>
    private async Task<(Schedule? Schedule, ScheduleError? Error)> FindOwnedAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        var found = await schedules.GetWithAccessAsync(scheduleId, currentUser.UserId, cancellationToken);
        if (found is null)
        {
            return (null, ScheduleNotFound);
        }

        return found.Access.Can(Permission.Manage) ? (found.Schedule, null) : (null, Forbidden);
    }

    /// <summary>Tylko do właściciela i osoby, której dotyczy zmiana – pozostali nie widzą, kto ma dostęp.</summary>
    private Task NotifyMemberChangeAsync(ScheduleChangeKind kind, Schedule schedule, Guid memberUserId) =>
        notifier.ScheduleChangedAsync(
            new ScheduleChange(schedule.Id, kind, ScheduleName: schedule.Name, MemberUserId: memberUserId),
            [schedule.UserId, memberUserId]);

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static ScheduleError? ValidateRole(ScheduleMemberRole? role) =>
        role is { } value && Enum.IsDefined(value) ? null : Invalid("Wybierz uprawnienia: podgląd albo edycja.");

    private static ScheduleError Invalid(string message) => new(ScheduleErrorType.Validation, message);
}
