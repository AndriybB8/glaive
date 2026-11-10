using StudyPlanner.Application.Common;
using StudyPlanner.Application.TodoLists;
using StudyPlanner.Application.Users;
using StudyPlanner.Domain.TodoLists;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.UnitTests.TodoLists;

public class TodoListMemberServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeUserRepository _users = new();
    private readonly FakeTodoListRepository _lists = new();
    private readonly FakeMemberRepository _members;
    private readonly FakeTodoListNotifier _notifier = new();
    private readonly TestUser _currentUser = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(Now));
    private readonly TodoListMemberService _service;

    private readonly User _owner;
    private readonly User _friend;
    private readonly TodoList _list;

    public TodoListMemberServiceTests()
    {
        _members = new FakeMemberRepository(_lists, _users);
        _service = new TodoListMemberService(_members, _lists, _users, _notifier, _currentUser, _time);

        _owner = _users.Add("owner@mail.com");
        _friend = _users.Add("friend@mail.com");
        _list = TodoList.Create(_owner.Id, "Na egzamin", Now);
        _lists.Lists.Add(_list);
        _lists.Members = _members.Members;
        _currentUser.UserId = _owner.Id;
    }

    private Task<TodoListResult<TodoListMemberDto>> InviteAsync(string? email = "friend@mail.com", TodoListMemberRole? role = TodoListMemberRole.Viewer) =>
        _service.InviteAsync(_list.Id, new InviteTodoListMemberRequest(email, role), CancellationToken.None);

    private async Task<TodoListAccess?> AccessOfFriendAsync() =>
        (await _lists.GetWithAccessAsync(_list.Id, _friend.Id, CancellationToken.None))?.Access;

    /// <summary>Zaproszenie od właściciela przyjęte przez _friend; bieżącym użytkownikiem zostaje _friend.</summary>
    private async Task AcceptedFriendAsync(TodoListMemberRole role = TodoListMemberRole.Viewer)
    {
        await InviteAsync(role: role);
        _currentUser.UserId = _friend.Id;
        await _service.AcceptInvitationAsync(_list.Id, CancellationToken.None);
    }

    [Theory]
    [InlineData(TodoListMemberRole.Viewer)]
    [InlineData(TodoListMemberRole.Editor)]
    public async Task Invite_CreatesPendingInvitationWithoutAccess(TodoListMemberRole role)
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

        Assert.Equal(TodoListErrorType.Validation, result.Error!.Type);
        Assert.Empty(_members.Members);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((TodoListMemberRole)99)]
    public async Task Invite_RequiresValidRole(TodoListMemberRole? role)
    {
        var result = await InviteAsync(role: role);

        Assert.Equal(TodoListErrorType.Validation, result.Error!.Type);
        Assert.Empty(_members.Members);
    }

    [Fact]
    public async Task Invite_UnknownEmail_IsNotFound()
    {
        var result = await InviteAsync("nikt@mail.com");

        Assert.Equal(TodoListErrorType.NotFound, result.Error!.Type);
        Assert.Empty(_members.Members);
    }

    [Fact]
    public async Task Invite_DisabledAccount_IsNotFound()
    {
        _users.Disable(_friend);

        var result = await InviteAsync();

        Assert.Equal(TodoListErrorType.NotFound, result.Error!.Type);
        Assert.Empty(_members.Members);
    }

    [Fact]
    public async Task Invite_Yourself_IsValidationError()
    {
        var result = await InviteAsync("owner@mail.com");

        Assert.Equal(TodoListErrorType.Validation, result.Error!.Type);
        Assert.Empty(_members.Members);
    }

    [Fact]
    public async Task Invite_SamePersonTwice_IsConflict()
    {
        await InviteAsync();

        var result = await InviteAsync(role: TodoListMemberRole.Editor);

        Assert.Equal(TodoListErrorType.Conflict, result.Error!.Type);
        Assert.Equal(TodoListMemberRole.Viewer, _members.Members.Single().Role);
    }

    [Fact]
    public async Task Invite_WhenListDeletedBeforeSave_IsNotFound()
    {
        _members.NextAddResult = AddTodoListMemberResult.ReferenceMissing;

        var result = await InviteAsync();

        Assert.Equal(TodoListErrorType.NotFound, result.Error!.Type);
    }

    [Theory]
    [InlineData(TodoListMemberRole.Viewer)]
    [InlineData(TodoListMemberRole.Editor)]
    public async Task Invite_ByMember_IsForbiddenEvenForUnknownEmail(TodoListMemberRole role)
    {
        await AcceptedFriendAsync(role);

        var known = await InviteAsync("owner@mail.com");
        var unknown = await InviteAsync("nikt@mail.com");

        Assert.Equal(TodoListErrorType.Forbidden, known.Error!.Type);
        Assert.Equal(TodoListErrorType.Forbidden, unknown.Error!.Type);
        Assert.Single(_members.Members);
    }

    [Fact]
    public async Task OwnerOperations_ByStrangerOrPendingInvitee_AreNotFound()
    {
        await InviteAsync();
        _currentUser.UserId = _friend.Id;

        var pendingList = await _service.GetByListAsync(_list.Id, CancellationToken.None);
        var pendingInvite = await InviteAsync("owner@mail.com");

        _currentUser.UserId = Guid.NewGuid();
        var strangerList = await _service.GetByListAsync(_list.Id, CancellationToken.None);
        var strangerChange = await _service.ChangeRoleAsync(_list.Id, _friend.Id, new ChangeTodoListMemberRoleRequest(TodoListMemberRole.Editor), CancellationToken.None);
        var strangerRemove = await _service.RemoveAsync(_list.Id, _friend.Id, CancellationToken.None);

        Assert.Equal(TodoListErrorType.NotFound, pendingList.Error!.Type);
        Assert.Equal(TodoListErrorType.NotFound, pendingInvite.Error!.Type);
        Assert.Equal(TodoListErrorType.NotFound, strangerList.Error!.Type);
        Assert.Equal(TodoListErrorType.NotFound, strangerChange.Error!.Type);
        Assert.Equal(TodoListErrorType.NotFound, strangerRemove.Error!.Type);
        Assert.Equal(TodoListMemberRole.Viewer, _members.Members.Single().Role);
    }

    [Fact]
    public async Task GetByList_ReturnsMembersAndPendingOldestFirst()
    {
        var second = _users.Add("second@mail.com");
        await InviteAsync();
        _time.Advance(TimeSpan.FromMinutes(1));
        await InviteAsync("second@mail.com", TodoListMemberRole.Editor);
        _currentUser.UserId = _friend.Id;
        await _service.AcceptInvitationAsync(_list.Id, CancellationToken.None);
        _currentUser.UserId = _owner.Id;

        var result = await _service.GetByListAsync(_list.Id, CancellationToken.None);

        Assert.Equal([_friend.Id, second.Id], result.Value!.Select(m => m.UserId));
        Assert.NotNull(result.Value![0].AcceptedAt);
        Assert.Null(result.Value[1].AcceptedAt);
    }

    [Fact]
    public async Task GetByList_ByMember_IsForbidden()
    {
        await AcceptedFriendAsync();

        var result = await _service.GetByListAsync(_list.Id, CancellationToken.None);

        Assert.Equal(TodoListErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task ChangeRole_ChangesAccessOfAcceptedMember()
    {
        await AcceptedFriendAsync();
        _currentUser.UserId = _owner.Id;

        var result = await _service.ChangeRoleAsync(_list.Id, _friend.Id, new ChangeTodoListMemberRoleRequest(TodoListMemberRole.Editor), CancellationToken.None);

        Assert.Equal(TodoListMemberRole.Editor, result.Value!.Role);
        Assert.Equal("friend@mail.com", result.Value.Email);
        Assert.Equal(TodoListAccess.Editor, await AccessOfFriendAsync());
    }

    [Fact]
    public async Task ChangeRole_ToSameRole_DoesNotSave()
    {
        await InviteAsync();

        var result = await _service.ChangeRoleAsync(_list.Id, _friend.Id, new ChangeTodoListMemberRoleRequest(TodoListMemberRole.Viewer), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _members.Updates);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((TodoListMemberRole)99)]
    public async Task ChangeRole_RequiresValidRole(TodoListMemberRole? role)
    {
        await InviteAsync();

        var result = await _service.ChangeRoleAsync(_list.Id, _friend.Id, new ChangeTodoListMemberRoleRequest(role), CancellationToken.None);

        Assert.Equal(TodoListErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task ChangeRole_ForPersonWithoutAccess_IsNotFound()
    {
        var result = await _service.ChangeRoleAsync(_list.Id, _friend.Id, new ChangeTodoListMemberRoleRequest(TodoListMemberRole.Editor), CancellationToken.None);

        Assert.Equal(TodoListErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task ChangeRole_WhenMemberRemovedBeforeSave_IsNotFound()
    {
        await InviteAsync();
        _members.FailNextUpdate = true;

        var result = await _service.ChangeRoleAsync(_list.Id, _friend.Id, new ChangeTodoListMemberRoleRequest(TodoListMemberRole.Editor), CancellationToken.None);

        Assert.Equal(TodoListErrorType.NotFound, result.Error!.Type);
    }

    [Theory]
    [InlineData(TodoListMemberRole.Viewer)]
    [InlineData(TodoListMemberRole.Editor)]
    public async Task ChangeRole_ByMember_IsForbidden(TodoListMemberRole role)
    {
        await AcceptedFriendAsync(role);

        var result = await _service.ChangeRoleAsync(_list.Id, _friend.Id, new ChangeTodoListMemberRoleRequest(TodoListMemberRole.Editor), CancellationToken.None);

        Assert.Equal(TodoListErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task Remove_ByOwner_TakesAccessAwayImmediately()
    {
        await AcceptedFriendAsync();
        _currentUser.UserId = _owner.Id;

        var result = await _service.RemoveAsync(_list.Id, _friend.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(await AccessOfFriendAsync());
    }

    [Fact]
    public async Task Remove_ByOwner_CancelsPendingInvitation()
    {
        await InviteAsync();

        var removed = await _service.RemoveAsync(_list.Id, _friend.Id, CancellationToken.None);
        _currentUser.UserId = _friend.Id;
        var accept = await _service.AcceptInvitationAsync(_list.Id, CancellationToken.None);

        Assert.True(removed.IsSuccess);
        Assert.Equal(TodoListErrorType.NotFound, accept.Error!.Type);
    }

    [Fact]
    public async Task Remove_OwnEntry_LeavesList()
    {
        await AcceptedFriendAsync();

        var result = await _service.RemoveAsync(_list.Id, _friend.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(await AccessOfFriendAsync());
    }

    [Fact]
    public async Task Remove_AnotherMember_ByMember_IsForbidden()
    {
        var other = _users.Add("other@mail.com");
        await InviteAsync("other@mail.com", TodoListMemberRole.Editor);
        await AcceptedFriendAsync(TodoListMemberRole.Editor);

        var result = await _service.RemoveAsync(_list.Id, other.Id, CancellationToken.None);

        Assert.Equal(TodoListErrorType.Forbidden, result.Error!.Type);
        Assert.Equal(2, _members.Members.Count);
    }

    [Fact]
    public async Task Remove_Owner_IsNotFoundBecauseOwnerHasNoEntry()
    {
        var result = await _service.RemoveAsync(_list.Id, _owner.Id, CancellationToken.None);

        Assert.Equal(TodoListErrorType.NotFound, result.Error!.Type);
        Assert.Contains(_list, _lists.Lists);
    }

    [Fact]
    public async Task GetInvitations_ReturnsOnlyOwnPendingNewestFirst()
    {
        var older = _list;
        var newer = TodoList.Create(_owner.Id, "Zakupy", Now);
        var accepted = TodoList.Create(_owner.Id, "Przyjęta", Now);
        _lists.Lists.Add(newer);
        _lists.Lists.Add(accepted);
        await InviteAsync(role: TodoListMemberRole.Editor);
        _time.Advance(TimeSpan.FromMinutes(1));
        await _service.InviteAsync(newer.Id, new InviteTodoListMemberRequest("friend@mail.com", TodoListMemberRole.Viewer), CancellationToken.None);
        await _service.InviteAsync(accepted.Id, new InviteTodoListMemberRequest("friend@mail.com", TodoListMemberRole.Viewer), CancellationToken.None);
        _currentUser.UserId = _friend.Id;
        await _service.AcceptInvitationAsync(accepted.Id, CancellationToken.None);

        var invitations = await _service.GetInvitationsAsync(CancellationToken.None);

        Assert.Equal([newer.Id, older.Id], invitations.Select(i => i.ListId));
        Assert.Equal("owner@mail.com", invitations[1].OwnerEmail);
        Assert.Equal("Na egzamin", invitations[1].ListTitle);
        Assert.Equal(TodoListMemberRole.Editor, invitations[1].Role);

        _currentUser.UserId = _owner.Id;
        Assert.Empty(await _service.GetInvitationsAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(TodoListMemberRole.Viewer, TodoListAccess.Viewer)]
    [InlineData(TodoListMemberRole.Editor, TodoListAccess.Editor)]
    public async Task Accept_GivesAccessFromRole(TodoListMemberRole role, TodoListAccess expected)
    {
        await InviteAsync(role: role);
        _currentUser.UserId = _friend.Id;
        _time.Advance(TimeSpan.FromHours(1));

        var result = await _service.AcceptInvitationAsync(_list.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, await AccessOfFriendAsync());
        Assert.Equal(Now.AddHours(1), _members.Members.Single().AcceptedAt);
    }

    [Fact]
    public async Task Accept_Twice_KeepsFirstDateAndDoesNotSaveAgain()
    {
        await AcceptedFriendAsync();
        var first = _members.Members.Single().AcceptedAt;
        var updates = _members.Updates;
        _time.Advance(TimeSpan.FromHours(1));

        var result = await _service.AcceptInvitationAsync(_list.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(first, _members.Members.Single().AcceptedAt);
        Assert.Equal(updates, _members.Updates);
    }

    [Fact]
    public async Task Accept_WithoutInvitation_IsNotFound()
    {
        _currentUser.UserId = _friend.Id;

        var result = await _service.AcceptInvitationAsync(_list.Id, CancellationToken.None);

        Assert.Equal(TodoListErrorType.NotFound, result.Error!.Type);
        Assert.Empty(_members.Members);
    }

    [Fact]
    public async Task Accept_WhenInvitationCancelledBeforeSave_IsNotFound()
    {
        await InviteAsync();
        _currentUser.UserId = _friend.Id;
        _members.FailNextUpdate = true;

        var result = await _service.AcceptInvitationAsync(_list.Id, CancellationToken.None);

        Assert.Equal(TodoListErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Decline_RemovesInvitationAndAllowsInvitingAgain()
    {
        await InviteAsync();
        _currentUser.UserId = _friend.Id;

        var declined = await _service.DeclineInvitationAsync(_list.Id, CancellationToken.None);
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

        var result = await _service.DeclineInvitationAsync(_list.Id, CancellationToken.None);

        Assert.Equal(TodoListErrorType.NotFound, result.Error!.Type);
        Assert.Equal(TodoListAccess.Viewer, await AccessOfFriendAsync());
    }

    private void AssertOnlyMemberChange(TodoListChangeKind kind)
    {
        var (change, recipients) = Assert.Single(_notifier.Changes);
        Assert.Equal(new TodoListChange(_list.Id, kind, ListTitle: "Na egzamin", MemberUserId: _friend.Id), change);
        Assert.Equal([_owner.Id, _friend.Id], recipients!);
    }

    [Fact]
    public async Task Invite_NotifiesOnlyTheInvitedPerson()
    {
        await InviteAsync();

        Assert.Equal((_friend.Id, _list.Id, "Na egzamin"), Assert.Single(_notifier.Invitations));
        Assert.Empty(_notifier.Changes);
    }

    [Fact]
    public async Task FailedInvite_NotifiesNobody()
    {
        await InviteAsync();
        _notifier.Invitations.Clear();

        await InviteAsync();
        await InviteAsync("nikt@mail.com");
        await InviteAsync("owner@mail.com");

        Assert.Empty(_notifier.Invitations);
    }

    [Fact]
    public async Task Accept_NotifiesOwnerAndNewMemberOnly()
    {
        await InviteAsync();
        _currentUser.UserId = _friend.Id;

        await _service.AcceptInvitationAsync(_list.Id, CancellationToken.None);

        AssertOnlyMemberChange(TodoListChangeKind.MemberJoined);
    }

    [Fact]
    public async Task Accept_Twice_NotifiesOnce()
    {
        await AcceptedFriendAsync();

        await _service.AcceptInvitationAsync(_list.Id, CancellationToken.None);

        Assert.Single(_notifier.Changes);
    }

    [Fact]
    public async Task ChangeRole_NotifiesOwnerAndThatMemberOnly()
    {
        await InviteAsync();

        await _service.ChangeRoleAsync(_list.Id, _friend.Id, new ChangeTodoListMemberRoleRequest(TodoListMemberRole.Editor), CancellationToken.None);

        AssertOnlyMemberChange(TodoListChangeKind.MemberRoleChanged);
    }

    [Fact]
    public async Task ChangeRole_ToSameRole_NotifiesNobody()
    {
        await InviteAsync();

        await _service.ChangeRoleAsync(_list.Id, _friend.Id, new ChangeTodoListMemberRoleRequest(TodoListMemberRole.Viewer), CancellationToken.None);

        Assert.Empty(_notifier.Changes);
    }

    [Fact]
    public async Task Remove_NotifiesRemovedMemberEvenThoughTheyLostAccess()
    {
        await InviteAsync();

        await _service.RemoveAsync(_list.Id, _friend.Id, CancellationToken.None);

        AssertOnlyMemberChange(TodoListChangeKind.MemberRemoved);
    }

    [Fact]
    public async Task Remove_WithoutEntry_NotifiesNobody()
    {
        await _service.RemoveAsync(_list.Id, _friend.Id, CancellationToken.None);

        Assert.Empty(_notifier.Changes);
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

    private sealed class FakeTodoListRepository : ITodoListRepository
    {
        public List<TodoList> Lists { get; } = [];

        public List<TodoListMember> Members { get; set; } = [];

        public Task<IReadOnlyList<TodoListSummary>> GetSummariesByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TodoListWithAccess?> GetWithAccessAsync(Guid id, Guid userId, CancellationToken cancellationToken)
        {
            var list = Lists.FirstOrDefault(l => l.Id == id);
            if (list is null)
            {
                return Task.FromResult<TodoListWithAccess?>(null);
            }

            var access = list.UserId == userId
                ? TodoListAccess.Owner
                : Members.FirstOrDefault(m => m.ListId == id && m.UserId == userId && m.AcceptedAt != null)?.Role.ToAccess();

            return Task.FromResult(access is { } value ? new TodoListWithAccess(list, value) : null);
        }

        public Task<IReadOnlyList<Guid>> GetUserIdsWithAccessAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(TodoList list, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task UpdateAsync(TodoList list, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(TodoList list, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeMemberRepository(FakeTodoListRepository lists, FakeUserRepository users) : ITodoListMemberRepository
    {
        public List<TodoListMember> Members { get; } = [];

        /// <summary>Wynik następnego dodania, zamiast sprawdzania duplikatu (jak przy usunięciu listy w trakcie zapisu).</summary>
        public AddTodoListMemberResult? NextAddResult { get; set; }

        /// <summary>Następny zapis zgłosi, że wpisu już nie ma.</summary>
        public bool FailNextUpdate { get; set; }

        public int Updates { get; private set; }

        public Task<IReadOnlyList<TodoListMemberWithEmail>> GetByListAsync(Guid listId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TodoListMemberWithEmail>>(Members.Where(m => m.ListId == listId).Select(WithEmail).ToList());

        public Task<TodoListMemberWithEmail?> GetAsync(Guid listId, Guid userId, CancellationToken cancellationToken)
        {
            var member = Members.FirstOrDefault(m => m.ListId == listId && m.UserId == userId);
            return Task.FromResult(member is null ? null : WithEmail(member));
        }

        public Task<IReadOnlyList<TodoListInvitationDto>> GetPendingByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TodoListInvitationDto>>(Members
                .Where(m => m.UserId == userId && m.AcceptedAt == null)
                .Select(m =>
                {
                    var list = lists.Lists.Single(l => l.Id == m.ListId);
                    var owner = users.Users.Single(u => u.Id == list.UserId);
                    return new TodoListInvitationDto(list.Id, list.Title, owner.Email, m.Role, m.CreatedAt);
                })
                .ToList());

        public Task<AddTodoListMemberResult> AddAsync(TodoListMember member, CancellationToken cancellationToken)
        {
            if (NextAddResult is { } forced)
            {
                NextAddResult = null;
                return Task.FromResult(forced);
            }

            if (Members.Any(m => m.ListId == member.ListId && m.UserId == member.UserId))
            {
                return Task.FromResult(AddTodoListMemberResult.AlreadyExists);
            }

            Members.Add(member);
            return Task.FromResult(AddTodoListMemberResult.Added);
        }

        public Task<bool> UpdateAsync(TodoListMember member, CancellationToken cancellationToken)
        {
            if (FailNextUpdate)
            {
                FailNextUpdate = false;
                return Task.FromResult(false);
            }

            Updates++;
            return Task.FromResult(true);
        }

        public Task<bool> RemoveAsync(Guid listId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Members.RemoveAll(m => m.ListId == listId && m.UserId == userId) > 0);

        private TodoListMemberWithEmail WithEmail(TodoListMember member) =>
            new(member, users.Users.Single(u => u.Id == member.UserId).Email);
    }
}
