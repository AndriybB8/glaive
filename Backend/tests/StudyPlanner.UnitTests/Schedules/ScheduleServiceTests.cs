using StudyPlanner.Application.Common;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Application.Users;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.UnitTests.Schedules;

public class ScheduleMemberServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Jan25 = new(2027, 1, 25);
    private static readonly DateOnly Feb14 = new(2027, 2, 14);

    private readonly FakeUserRepository _users = new();
    private readonly FakeScheduleRepository _schedules = new();
    private readonly FakeMemberRepository _members;
    private readonly TestUser _currentUser = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(Now));
    private readonly ScheduleMemberService _service;

    private readonly User _owner;
    private readonly User _friend;
    private readonly Schedule _schedule;

    public ScheduleMemberServiceTests()
    {
        _members = new FakeMemberRepository(_schedules, _users);
        _service = new ScheduleMemberService(_members, _schedules, _users, _currentUser, _time);

        _owner = _users.Add("owner@mail.com");
        _friend = _users.Add("friend@mail.com");
        _schedule = Schedule.Create(_owner.Id, "Sesja zimowa", Jan25, Feb14, Now);
        _schedules.Schedules.Add(_schedule);
        _schedules.Members = _members.Members;
        _currentUser.UserId = _owner.Id;
    }

    private Task<ScheduleResult<ScheduleMemberDto>> InviteAsync(string? email = "friend@mail.com", ScheduleMemberRole? role = ScheduleMemberRole.Viewer) =>
        _service.InviteAsync(_schedule.Id, new InviteScheduleMemberRequest(email, role), CancellationToken.None);

    private async Task<ScheduleAccess?> AccessOfFriendAsync() =>
        (await _schedules.GetWithAccessAsync(_schedule.Id, _friend.Id, CancellationToken.None))?.Access;

    /// <summary>Zaproszenie od właściciela przyjęte przez _friend; bieżącym użytkownikiem zostaje _friend.</summary>
    private async Task AcceptedFriendAsync(ScheduleMemberRole role = ScheduleMemberRole.Viewer)
    {
        await InviteAsync(role: role);
        _currentUser.UserId = _friend.Id;
        await _service.AcceptInvitationAsync(_schedule.Id, CancellationToken.None);
    }

    [Theory]
    [InlineData(ScheduleMemberRole.Viewer)]
    [InlineData(ScheduleMemberRole.Editor)]
    public async Task Invite_CreatesPendingInvitationWithoutAccess(ScheduleMemberRole role)
    {
        var result = await InviteAsync(role: role);

        Assert.True(result.IsSuccess);
        Assert.Equal(_friend.Id, result.Value!.UserId);
        Assert.Equal("friend@mail.com", result.Value.Email);
        Assert.Equal(role, result.Value.Role);
        Assert.Equal(Now, result.Value.CreatedAt);
        Assert.Null(result.Value.AcceptedAt);
        Assert.Null(await AccessOfFriendAsync());
    }

    [Fact]
    public async Task Invite_NormalizesEmail()
    {
        var result = await InviteAsync("  FRIEND@Mail.com ");

        Assert.Equal(_friend.Id, result.Value!.UserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Invite_RequiresEmail(string? email)
    {
        var result = await InviteAsync(email);

        Assert.Equal(ScheduleErrorType.Validation, result.Error!.Type);
        Assert.Empty(_members.Members);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((ScheduleMemberRole)99)]
    public async Task Invite_RequiresValidRole(ScheduleMemberRole? role)
    {
        var result = await InviteAsync(role: role);

        Assert.Equal(ScheduleErrorType.Validation, result.Error!.Type);
        Assert.Empty(_members.Members);
    }

    [Fact]
    public async Task Invite_UnknownEmail_IsNotFound()
    {
        var result = await InviteAsync("nikt@mail.com");

        Assert.Equal(ScheduleErrorType.NotFound, result.Error!.Type);
        Assert.Empty(_members.Members);
    }

    [Fact]
    public async Task Invite_DisabledAccount_IsNotFound()
    {
        _users.Disable(_friend);

        var result = await InviteAsync();

        Assert.Equal(ScheduleErrorType.NotFound, result.Error!.Type);
        Assert.Empty(_members.Members);
    }

    [Fact]
    public async Task Invite_Yourself_IsValidationError()
    {
        var result = await InviteAsync("owner@mail.com");

        Assert.Equal(ScheduleErrorType.Validation, result.Error!.Type);
        Assert.Empty(_members.Members);
    }

    [Fact]
    public async Task Invite_SamePersonTwice_IsConflict()
    {
        await InviteAsync();

        var result = await InviteAsync(role: ScheduleMemberRole.Editor);

        Assert.Equal(ScheduleErrorType.Conflict, result.Error!.Type);
        Assert.Equal(ScheduleMemberRole.Viewer, _members.Members.Single().Role);
    }

    [Fact]
    public async Task Invite_WhenScheduleDeletedBeforeSave_IsNotFound()
    {
        _members.NextAddResult = AddScheduleMemberResult.ReferenceMissing;

        var result = await InviteAsync();

        Assert.Equal(ScheduleErrorType.NotFound, result.Error!.Type);
    }

    [Theory]
    [InlineData(ScheduleMemberRole.Viewer)]
    [InlineData(ScheduleMemberRole.Editor)]
    public async Task Invite_ByMember_IsForbiddenEvenForUnknownEmail(ScheduleMemberRole role)
    {
        await AcceptedFriendAsync(role);

        var known = await InviteAsync("owner@mail.com");
        var unknown = await InviteAsync("nikt@mail.com");

        Assert.Equal(ScheduleErrorType.Forbidden, known.Error!.Type);
        Assert.Equal(ScheduleErrorType.Forbidden, unknown.Error!.Type);
        Assert.Single(_members.Members);
    }

    [Fact]
    public async Task OwnerOperations_ByStrangerOrPendingInvitee_AreNotFound()
    {
        await InviteAsync();
        _currentUser.UserId = _friend.Id;

        var pendingList = await _service.GetByScheduleAsync(_schedule.Id, CancellationToken.None);
        var pendingInvite = await InviteAsync("owner@mail.com");

        _currentUser.UserId = Guid.NewGuid();
        var strangerList = await _service.GetByScheduleAsync(_schedule.Id, CancellationToken.None);
        var strangerChange = await _service.ChangeRoleAsync(_schedule.Id, _friend.Id, new ChangeScheduleMemberRoleRequest(ScheduleMemberRole.Editor), CancellationToken.None);
        var strangerRemove = await _service.RemoveAsync(_schedule.Id, _friend.Id, CancellationToken.None);

        Assert.Equal(ScheduleErrorType.NotFound, pendingList.Error!.Type);
        Assert.Equal(ScheduleErrorType.NotFound, pendingInvite.Error!.Type);
        Assert.Equal(ScheduleErrorType.NotFound, strangerList.Error!.Type);
        Assert.Equal(ScheduleErrorType.NotFound, strangerChange.Error!.Type);
        Assert.Equal(ScheduleErrorType.NotFound, strangerRemove.Error!.Type);
        Assert.Equal(ScheduleMemberRole.Viewer, _members.Members.Single().Role);
    }

    [Fact]
    public async Task GetBySchedule_ReturnsMembersAndPendingOldestFirst()
    {
        var second = _users.Add("second@mail.com");
        await InviteAsync();
        _time.Advance(TimeSpan.FromMinutes(1));
        await InviteAsync("second@mail.com", ScheduleMemberRole.Editor);
        _currentUser.UserId = _friend.Id;
        await _service.AcceptInvitationAsync(_schedule.Id, CancellationToken.None);
        _currentUser.UserId = _owner.Id;

        var result = await _service.GetByScheduleAsync(_schedule.Id, CancellationToken.None);

        Assert.Equal([_friend.Id, second.Id], result.Value!.Select(m => m.UserId));
        Assert.NotNull(result.Value![0].AcceptedAt);
        Assert.Null(result.Value[1].AcceptedAt);
    }

    [Fact]
    public async Task GetBySchedule_ByMember_IsForbidden()
    {
        await AcceptedFriendAsync();

        var result = await _service.GetByScheduleAsync(_schedule.Id, CancellationToken.None);

        Assert.Equal(ScheduleErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task ChangeRole_ChangesAccessOfAcceptedMember()
    {
        await AcceptedFriendAsync();
        _currentUser.UserId = _owner.Id;

        var result = await _service.ChangeRoleAsync(_schedule.Id, _friend.Id, new ChangeScheduleMemberRoleRequest(ScheduleMemberRole.Editor), CancellationToken.None);

        Assert.Equal(ScheduleMemberRole.Editor, result.Value!.Role);
        Assert.Equal("friend@mail.com", result.Value.Email);
        Assert.Equal(ScheduleAccess.Editor, await AccessOfFriendAsync());
    }

    [Fact]
    public async Task ChangeRole_ToSameRole_DoesNotSave()
    {
        await InviteAsync();

        var result = await _service.ChangeRoleAsync(_schedule.Id, _friend.Id, new ChangeScheduleMemberRoleRequest(ScheduleMemberRole.Viewer), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _members.Updates);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((ScheduleMemberRole)99)]
    public async Task ChangeRole_RequiresValidRole(ScheduleMemberRole? role)
    {
        await InviteAsync();

        var result = await _service.ChangeRoleAsync(_schedule.Id, _friend.Id, new ChangeScheduleMemberRoleRequest(role), CancellationToken.None);

        Assert.Equal(ScheduleErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task ChangeRole_ForPersonWithoutAccess_IsNotFound()
    {
        var result = await _service.ChangeRoleAsync(_schedule.Id, _friend.Id, new ChangeScheduleMemberRoleRequest(ScheduleMemberRole.Editor), CancellationToken.None);

        Assert.Equal(ScheduleErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task ChangeRole_WhenMemberRemovedBeforeSave_IsNotFound()
    {
        await InviteAsync();
        _members.FailNextUpdate = true;

        var result = await _service.ChangeRoleAsync(_schedule.Id, _friend.Id, new ChangeScheduleMemberRoleRequest(ScheduleMemberRole.Editor), CancellationToken.None);

        Assert.Equal(ScheduleErrorType.NotFound, result.Error!.Type);
    }

    [Theory]
    [InlineData(ScheduleMemberRole.Viewer)]
    [InlineData(ScheduleMemberRole.Editor)]
    public async Task ChangeRole_ByMember_IsForbidden(ScheduleMemberRole role)
    {
        await AcceptedFriendAsync(role);

        var result = await _service.ChangeRoleAsync(_schedule.Id, _friend.Id, new ChangeScheduleMemberRoleRequest(ScheduleMemberRole.Editor), CancellationToken.None);

        Assert.Equal(ScheduleErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task Remove_ByOwner_TakesAccessAwayImmediately()
    {
        await AcceptedFriendAsync();
        _currentUser.UserId = _owner.Id;

        var result = await _service.RemoveAsync(_schedule.Id, _friend.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(await AccessOfFriendAsync());
    }

    [Fact]
    public async Task Remove_ByOwner_CancelsPendingInvitation()
    {
        await InviteAsync();

        var removed = await _service.RemoveAsync(_schedule.Id, _friend.Id, CancellationToken.None);
        _currentUser.UserId = _friend.Id;
        var accept = await _service.AcceptInvitationAsync(_schedule.Id, CancellationToken.None);

        Assert.True(removed.IsSuccess);
        Assert.Equal(ScheduleErrorType.NotFound, accept.Error!.Type);
    }

    [Fact]
    public async Task Remove_OwnEntry_LeavesSchedule()
    {
        await AcceptedFriendAsync();

        var result = await _service.RemoveAsync(_schedule.Id, _friend.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(await AccessOfFriendAsync());
    }

    [Fact]
    public async Task Remove_AnotherMember_ByMember_IsForbidden()
    {
        var other = _users.Add("other@mail.com");
        await InviteAsync("other@mail.com", ScheduleMemberRole.Editor);
        await AcceptedFriendAsync(ScheduleMemberRole.Editor);

        var result = await _service.RemoveAsync(_schedule.Id, other.Id, CancellationToken.None);

        Assert.Equal(ScheduleErrorType.Forbidden, result.Error!.Type);
        Assert.Equal(2, _members.Members.Count);
    }

    [Fact]
    public async Task Remove_Owner_IsNotFoundBecauseOwnerHasNoEntry()
    {
        var result = await _service.RemoveAsync(_schedule.Id, _owner.Id, CancellationToken.None);

        Assert.Equal(ScheduleErrorType.NotFound, result.Error!.Type);
        Assert.Contains(_schedule, _schedules.Schedules);
    }

    [Fact]
    public async Task GetInvitations_ReturnsOnlyOwnPendingNewestFirst()
    {
        var older = _schedule;
        var newer = Schedule.Create(_owner.Id, "Zakupy", Jan25, Feb14, Now);
        var accepted = Schedule.Create(_owner.Id, "Przyjęta", Jan25, Feb14, Now);
        _schedules.Schedules.Add(newer);
        _schedules.Schedules.Add(accepted);
        await InviteAsync(role: ScheduleMemberRole.Editor);
        _time.Advance(TimeSpan.FromMinutes(1));
        await _service.InviteAsync(newer.Id, new InviteScheduleMemberRequest("friend@mail.com", ScheduleMemberRole.Viewer), CancellationToken.None);
        await _service.InviteAsync(accepted.Id, new InviteScheduleMemberRequest("friend@mail.com", ScheduleMemberRole.Viewer), CancellationToken.None);
        _currentUser.UserId = _friend.Id;
        await _service.AcceptInvitationAsync(accepted.Id, CancellationToken.None);

        var invitations = await _service.GetInvitationsAsync(CancellationToken.None);

        Assert.Equal([newer.Id, older.Id], invitations.Select(i => i.ScheduleId));
        Assert.Equal("owner@mail.com", invitations[1].OwnerEmail);
        Assert.Equal("Sesja zimowa", invitations[1].ScheduleName);
        Assert.Equal(ScheduleMemberRole.Editor, invitations[1].Role);

        _currentUser.UserId = _owner.Id;
        Assert.Empty(await _service.GetInvitationsAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(ScheduleMemberRole.Viewer, ScheduleAccess.Viewer)]
    [InlineData(ScheduleMemberRole.Editor, ScheduleAccess.Editor)]
    public async Task Accept_GivesAccessFromRole(ScheduleMemberRole role, ScheduleAccess expected)
    {
        await InviteAsync(role: role);
        _currentUser.UserId = _friend.Id;
        _time.Advance(TimeSpan.FromHours(1));

        var result = await _service.AcceptInvitationAsync(_schedule.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, await AccessOfFriendAsync());
        Assert.Equal(Now.AddHours(1), _members.Members.Single().AcceptedAt);
    }

    [Fact]
    public async Task SchedulesList_ShowsOwnScheduleAsOwner()
    {
        var schedules = new ScheduleService(_schedules, _currentUser, _time);

        var listed = Assert.Single(await schedules.GetAllAsync(CancellationToken.None));

        Assert.Equal(_schedule.Id, listed.Id);
        Assert.Equal(ScheduleAccess.Owner, listed.Access);
    }

    [Theory]
    [InlineData(ScheduleMemberRole.Viewer, ScheduleAccess.Viewer)]
    [InlineData(ScheduleMemberRole.Editor, ScheduleAccess.Editor)]
    public async Task SchedulesList_ShowsSharedScheduleOnlyAfterAccepting(ScheduleMemberRole role, ScheduleAccess expected)
    {
        var schedules = new ScheduleService(_schedules, _currentUser, _time);
        await InviteAsync(role: role);
        _currentUser.UserId = _friend.Id;

        Assert.Empty(await schedules.GetAllAsync(CancellationToken.None));
        Assert.False((await schedules.GetByIdAsync(_schedule.Id, CancellationToken.None)).IsSuccess);

        await _service.AcceptInvitationAsync(_schedule.Id, CancellationToken.None);

        var listed = Assert.Single(await schedules.GetAllAsync(CancellationToken.None));
        Assert.Equal(_schedule.Id, listed.Id);
        Assert.Equal(expected, listed.Access);
        var byId = await schedules.GetByIdAsync(_schedule.Id, CancellationToken.None);
        Assert.True(byId.IsSuccess);
        Assert.Equal(expected, byId.Value!.Access);
    }

    [Fact]
    public async Task SchedulesList_AfterLeaving_NoLongerShowsSharedSchedule()
    {
        var schedules = new ScheduleService(_schedules, _currentUser, _time);
        await AcceptedFriendAsync();

        await _service.RemoveAsync(_schedule.Id, _friend.Id, CancellationToken.None);

        Assert.Empty(await schedules.GetAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Accept_Twice_KeepsFirstDateAndDoesNotSaveAgain()
    {
        await AcceptedFriendAsync();
        var first = _members.Members.Single().AcceptedAt;
        var updates = _members.Updates;
        _time.Advance(TimeSpan.FromHours(1));

        var result = await _service.AcceptInvitationAsync(_schedule.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(first, _members.Members.Single().AcceptedAt);
        Assert.Equal(updates, _members.Updates);
    }

    [Fact]
    public async Task Accept_WithoutInvitation_IsNotFound()
    {
        _currentUser.UserId = _friend.Id;

        var result = await _service.AcceptInvitationAsync(_schedule.Id, CancellationToken.None);

        Assert.Equal(ScheduleErrorType.NotFound, result.Error!.Type);
        Assert.Empty(_members.Members);
    }

    [Fact]
    public async Task Accept_WhenInvitationCancelledBeforeSave_IsNotFound()
    {
        await InviteAsync();
        _currentUser.UserId = _friend.Id;
        _members.FailNextUpdate = true;

        var result = await _service.AcceptInvitationAsync(_schedule.Id, CancellationToken.None);

        Assert.Equal(ScheduleErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Decline_RemovesInvitationAndAllowsInvitingAgain()
    {
        await InviteAsync();
        _currentUser.UserId = _friend.Id;

        var declined = await _service.DeclineInvitationAsync(_schedule.Id, CancellationToken.None);
        var invitations = await _service.GetInvitationsAsync(CancellationToken.None);
        _currentUser.UserId = _owner.Id;
        var again = await InviteAsync();

        Assert.True(declined.IsSuccess);
        Assert.Empty(invitations);
        Assert.True(again.IsSuccess);
    }

    [Fact]
    public async Task Decline_AfterAccepting_IsNotFoundAndKeepsAccess()
    {
        await AcceptedFriendAsync();

        var result = await _service.DeclineInvitationAsync(_schedule.Id, CancellationToken.None);

        Assert.Equal(ScheduleErrorType.NotFound, result.Error!.Type);
        Assert.Equal(ScheduleAccess.Viewer, await AccessOfFriendAsync());
    }

    private sealed class TestUser : ICurrentUser
    {
        public Guid UserId { get; set; }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];

        public User Add(string email)
        {
            var user = User.Create(email, "hash", Now);
            Users.Add(user);
            return user;
        }

        /// <summary>Konta nie da się jeszcze wyłączyć przez API domeny.</summary>
        public void Disable(User user) => typeof(User).GetProperty(nameof(User.Status))!.SetValue(user, AccountStatus.Disabled);

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Users.Any(u => u.Email == email));

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Email == email));
    }

    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public List<Schedule> Schedules { get; } = [];

        public List<ScheduleMember> Members { get; set; } = [];

        public Task<IReadOnlyList<ScheduleWithAccess>> GetAccessibleByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ScheduleWithAccess>>(Schedules
                .Select(s => s.UserId == userId
                    ? new ScheduleWithAccess(s, ScheduleAccess.Owner)
                    : Members.FirstOrDefault(m => m.ScheduleId == s.Id && m.UserId == userId && m.AcceptedAt != null) is { } member
                        ? new ScheduleWithAccess(s, member.Role.ToAccess())
                        : null)
                .OfType<ScheduleWithAccess>()
                .ToList());

        public Task<Schedule?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(Schedule schedule, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task UpdateAsync(Schedule schedule, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(Schedule schedule, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ScheduleWithAccess?> GetWithAccessAsync(Guid id, Guid userId, CancellationToken cancellationToken)
        {
            var schedule = Schedules.FirstOrDefault(s => s.Id == id);
            if (schedule is null)
            {
                return Task.FromResult<ScheduleWithAccess?>(null);
            }

            var access = schedule.UserId == userId
                ? ScheduleAccess.Owner
                : Members.FirstOrDefault(m => m.ScheduleId == id && m.UserId == userId && m.AcceptedAt != null)?.Role.ToAccess();

            return Task.FromResult(access is { } value ? new ScheduleWithAccess(schedule, value) : null);
        }
    }

    private sealed class FakeMemberRepository(FakeScheduleRepository schedules, FakeUserRepository users) : IScheduleMemberRepository
    {
        public List<ScheduleMember> Members { get; } = [];

        /// <summary>Wynik następnego dodania, zamiast sprawdzania duplikatu (jak przy usunięciu harmonogramu w trakcie zapisu).</summary>
        public AddScheduleMemberResult? NextAddResult { get; set; }

        /// <summary>Następny zapis zgłosi, że wpisu już nie ma.</summary>
        public bool FailNextUpdate { get; set; }

        public int Updates { get; private set; }

        public Task<IReadOnlyList<ScheduleMemberWithEmail>> GetByScheduleAsync(Guid scheduleId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ScheduleMemberWithEmail>>(Members.Where(m => m.ScheduleId == scheduleId).Select(WithEmail).ToList());

        public Task<ScheduleMemberWithEmail?> GetAsync(Guid scheduleId, Guid userId, CancellationToken cancellationToken)
        {
            var member = Members.FirstOrDefault(m => m.ScheduleId == scheduleId && m.UserId == userId);
            return Task.FromResult(member is null ? null : WithEmail(member));
        }

        public Task<IReadOnlyList<ScheduleInvitationDto>> GetPendingByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ScheduleInvitationDto>>(Members
                .Where(m => m.UserId == userId && m.AcceptedAt == null)
                .Select(m =>
                {
                    var schedule = schedules.Schedules.Single(s => s.Id == m.ScheduleId);
                    var owner = users.Users.Single(u => u.Id == schedule.UserId);
                    return new ScheduleInvitationDto(schedule.Id, schedule.Name, owner.Email, m.Role, m.CreatedAt);
                })
                .ToList());

        public Task<AddScheduleMemberResult> AddAsync(ScheduleMember member, CancellationToken cancellationToken)
        {
            if (NextAddResult is { } forced)
            {
                NextAddResult = null;
                return Task.FromResult(forced);
            }

            if (Members.Any(m => m.ScheduleId == member.ScheduleId && m.UserId == member.UserId))
            {
                return Task.FromResult(AddScheduleMemberResult.AlreadyExists);
            }

            Members.Add(member);
            return Task.FromResult(AddScheduleMemberResult.Added);
        }

        public Task<bool> UpdateAsync(ScheduleMember member, CancellationToken cancellationToken)
        {
            if (FailNextUpdate)
            {
                FailNextUpdate = false;
                return Task.FromResult(false);
            }

            Updates++;
            return Task.FromResult(true);
        }

        public Task<bool> RemoveAsync(Guid scheduleId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Members.RemoveAll(m => m.ScheduleId == scheduleId && m.UserId == userId) > 0);

        private ScheduleMemberWithEmail WithEmail(ScheduleMember member) =>
            new(member, users.Users.Single(u => u.Id == member.UserId).Email);
    }
}
