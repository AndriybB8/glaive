using StudyPlanner.Application.Common;
using StudyPlanner.Application.TodoLists;
using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.UnitTests.TodoLists;

public class TodoListServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly FakeTodoListRepository _repository = new();
    private readonly FakeTodoListNotifier _notifier = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly TodoListService _service;

    public TodoListServiceTests()
    {
        _service = new TodoListService(_repository, _notifier, new TestUser(UserId), _time);
    }

    private async Task<TodoListDto> CreateAsync(string title = "Na egzamin") =>
        (await _service.CreateAsync(new CreateTodoListRequest(title), CancellationToken.None)).Value!;

    [Fact]
    public async Task Create_TrimsTitleAndSetsOwnerUtcDatesAndFirstVersion()
    {
        var result = await _service.CreateAsync(new CreateTodoListRequest("  Na egzamin z baz  "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var list = result.Value!;
        Assert.Equal("Na egzamin z baz", list.Title);
        Assert.Equal(1, list.Version);
        Assert.Equal(DateTimeKind.Utc, list.CreatedAt.Kind);
        Assert.Equal(list.CreatedAt, list.UpdatedAt);
        Assert.Equal(UserId, _repository.Lists.Single().UserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Create_RejectsEmptyTitle(string? title)
    {
        var result = await _service.CreateAsync(new CreateTodoListRequest(title), CancellationToken.None);

        Assert.Equal(TodoListErrorType.Validation, result.Error!.Type);
        Assert.Equal("Podaj nazwę listy.", result.Error.Message);
        Assert.Empty(_repository.Lists);
    }

    [Fact]
    public async Task Create_RejectsTooLongTitle()
    {
        var title = new string('a', TodoList.TitleMaxLength + 1);

        var result = await _service.CreateAsync(new CreateTodoListRequest(title), CancellationToken.None);

        Assert.Equal(TodoListErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Create_AllowsDuplicateTitle()
    {
        await CreateAsync("Zakupy");

        var result = await _service.CreateAsync(new CreateTodoListRequest("Zakupy"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _repository.Lists.Count);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyOwnListsSortedWithItemCounts()
    {
        var zakupy = await CreateAsync("zakupy");
        var egzamin = await CreateAsync("Egzamin");
        _repository.Lists.Add(TodoList.Create(Guid.NewGuid(), "Cudza lista", DateTime.UtcNow));

        var done = TodoItem.Create(UserId, egzamin.Id, "Powtórzyć SQL", 0, DateTime.UtcNow);
        done.SetDone(true, DateTime.UtcNow);
        _repository.Items.Add(done);
        _repository.Items.Add(TodoItem.Create(UserId, egzamin.Id, "Normalizacja", 1, DateTime.UtcNow));

        var lists = await _service.GetAllAsync(CancellationToken.None);

        Assert.Equal(["Egzamin", "zakupy"], lists.Select(l => l.Title));
        Assert.Equal((2, 1), (lists[0].ItemCount, lists[0].DoneCount));
        Assert.Equal((0, 0), (lists[1].ItemCount, lists[1].DoneCount));
        Assert.Equal(zakupy.Id, lists[1].Id);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundForListOfAnotherUser()
    {
        var foreign = TodoList.Create(Guid.NewGuid(), "Cudza lista", DateTime.UtcNow);
        _repository.Lists.Add(foreign);

        var result = await _service.GetByIdAsync(foreign.Id, CancellationToken.None);

        Assert.Equal(TodoListErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Update_WithCurrentVersion_RenamesAndIncrementsVersion()
    {
        var created = await CreateAsync("Zakupy");
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.UpdateAsync(created.Id, new UpdateTodoListRequest(" Zakupy przed sesją ", created.Version), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Zakupy przed sesją", result.Value!.Title);
        Assert.Equal(2, result.Value.Version);
        Assert.True(result.Value.UpdatedAt > result.Value.CreatedAt);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ReturnsConflictWithCurrentList()
    {
        var created = await CreateAsync("Zakupy");
        await _service.UpdateAsync(created.Id, new UpdateTodoListRequest("Zakupy 2", created.Version), CancellationToken.None);

        var result = await _service.UpdateAsync(created.Id, new UpdateTodoListRequest("Zakupy 3", created.Version), CancellationToken.None);

        Assert.Equal(TodoListErrorType.Conflict, result.Error!.Type);
        Assert.Equal("Zakupy 2", result.Error.Current!.Title);
    }

    [Fact]
    public async Task Update_WithoutChanges_DoesNotIncrementVersion()
    {
        var created = await CreateAsync("Zakupy");

        var result = await _service.UpdateAsync(created.Id, new UpdateTodoListRequest("Zakupy", created.Version), CancellationToken.None);

        Assert.Equal(1, result.Value!.Version);
    }

    [Fact]
    public async Task Update_RejectsEmptyTitle()
    {
        var created = await CreateAsync("Zakupy");

        var result = await _service.UpdateAsync(created.Id, new UpdateTodoListRequest(" ", created.Version), CancellationToken.None);

        Assert.Equal(TodoListErrorType.Validation, result.Error!.Type);
        Assert.Equal("Zakupy", _repository.Lists.Single().Title);
    }

    [Fact]
    public async Task Update_ReturnsNotFoundForMissingList()
    {
        var result = await _service.UpdateAsync(Guid.NewGuid(), new UpdateTodoListRequest("Zakupy", 1), CancellationToken.None);

        Assert.Equal(TodoListErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Delete_RemovesOwnList()
    {
        var created = await CreateAsync();

        var result = await _service.DeleteAsync(created.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_repository.Lists);
    }

    [Fact]
    public async Task Delete_DoesNotRemoveListOfAnotherUser()
    {
        var foreign = TodoList.Create(Guid.NewGuid(), "Cudza lista", DateTime.UtcNow);
        _repository.Lists.Add(foreign);

        var result = await _service.DeleteAsync(foreign.Id, CancellationToken.None);

        Assert.Equal(TodoListErrorType.NotFound, result.Error!.Type);
        Assert.Single(_repository.Lists);
    }

    [Fact]
    public async Task Rename_NotifiesPeopleWithAccess()
    {
        var created = await CreateAsync("Zakupy");

        await _service.UpdateAsync(created.Id, new UpdateTodoListRequest("Zakupy 2", created.Version), CancellationToken.None);

        var (change, recipients) = Assert.Single(_notifier.Changes);
        Assert.Equal(new TodoListChange(created.Id, TodoListChangeKind.ListRenamed, ListTitle: "Zakupy 2"), change);
        Assert.Null(recipients);
    }

    [Fact]
    public async Task Rename_NotifiesNobodyWhenNothingWasSaved()
    {
        var created = await CreateAsync("Zakupy");
        var shared = SharedList(TodoListMemberRole.Editor);

        await _service.UpdateAsync(created.Id, new UpdateTodoListRequest("Zakupy", created.Version), CancellationToken.None);
        await _service.UpdateAsync(created.Id, new UpdateTodoListRequest("Zakupy 2", created.Version + 1), CancellationToken.None);
        await _service.UpdateAsync(shared.Id, new UpdateTodoListRequest("Cudza 2", shared.Version), CancellationToken.None);

        Assert.Empty(_notifier.Changes);
    }

    [Fact]
    public async Task Delete_NotifiesEveryoneWhoHadAccessBeforeTheCascade()
    {
        var created = await CreateAsync("Zakupy");
        var memberId = Guid.NewGuid();
        var member = TodoListMember.Invite(created.Id, memberId, TodoListMemberRole.Viewer, DateTime.UtcNow);
        member.Accept(DateTime.UtcNow);
        _repository.Members.Add(member);

        await _service.DeleteAsync(created.Id, CancellationToken.None);

        var (change, recipients) = Assert.Single(_notifier.Changes);
        Assert.Equal(new TodoListChange(created.Id, TodoListChangeKind.ListDeleted, ListTitle: "Zakupy"), change);
        Assert.Equal([UserId, memberId], recipients!);
    }

    [Fact]
    public async Task Delete_NotifiesNobodyWhenForbidden()
    {
        var shared = SharedList(TodoListMemberRole.Editor);

        await _service.DeleteAsync(shared.Id, CancellationToken.None);

        Assert.Empty(_notifier.Changes);
    }

    /// <summary>Lista innej osoby, udostępniona bieżącemu użytkownikowi.</summary>
    private TodoList SharedList(TodoListMemberRole role, bool accepted = true, string title = "Cudza lista")
    {
        var list = TodoList.Create(Guid.NewGuid(), title, DateTime.UtcNow);
        _repository.Lists.Add(list);

        var member = TodoListMember.Invite(list.Id, UserId, role, DateTime.UtcNow);
        if (accepted)
        {
            member.Accept(DateTime.UtcNow);
        }

        _repository.Members.Add(member);
        return list;
    }

    [Fact]
    public async Task OwnList_HasOwnerAccess()
    {
        var created = await CreateAsync();

        var byId = await _service.GetByIdAsync(created.Id, CancellationToken.None);
        var all = await _service.GetAllAsync(CancellationToken.None);

        Assert.Equal(TodoListAccess.Owner, created.Access);
        Assert.Equal(TodoListAccess.Owner, byId.Value!.Access);
        Assert.Equal(TodoListAccess.Owner, all.Single().Access);
    }

    [Theory]
    [InlineData(TodoListMemberRole.Viewer, TodoListAccess.Viewer)]
    [InlineData(TodoListMemberRole.Editor, TodoListAccess.Editor)]
    public async Task SharedList_IsVisibleWithAccessFromRole(TodoListMemberRole role, TodoListAccess expected)
    {
        var shared = SharedList(role);

        var byId = await _service.GetByIdAsync(shared.Id, CancellationToken.None);
        var all = await _service.GetAllAsync(CancellationToken.None);

        Assert.Equal(expected, byId.Value!.Access);
        Assert.Equal(expected, all.Single().Access);
    }

    [Fact]
    public async Task SharedList_WithPendingInvitation_IsNotVisible()
    {
        var pending = SharedList(TodoListMemberRole.Editor, accepted: false);

        var byId = await _service.GetByIdAsync(pending.Id, CancellationToken.None);
        var all = await _service.GetAllAsync(CancellationToken.None);

        Assert.Equal(TodoListErrorType.NotFound, byId.Error!.Type);
        Assert.Empty(all);
    }

    [Theory]
    [InlineData(TodoListMemberRole.Viewer)]
    [InlineData(TodoListMemberRole.Editor)]
    public async Task SharedList_OnlyOwnerCanRenameOrDelete(TodoListMemberRole role)
    {
        var shared = SharedList(role, title: "Oryginalna");

        var rename = await _service.UpdateAsync(shared.Id, new UpdateTodoListRequest("Zmieniona", shared.Version), CancellationToken.None);
        var delete = await _service.DeleteAsync(shared.Id, CancellationToken.None);

        Assert.Equal(TodoListErrorType.Forbidden, rename.Error!.Type);
        Assert.Equal(TodoListErrorType.Forbidden, delete.Error!.Type);
        Assert.Equal("Oryginalna", shared.Title);
        Assert.Contains(shared, _repository.Lists);
    }

    [Fact]
    public async Task SharedList_WithPendingInvitation_RenameAndDeleteAreNotFound()
    {
        var pending = SharedList(TodoListMemberRole.Editor, accepted: false);

        var rename = await _service.UpdateAsync(pending.Id, new UpdateTodoListRequest("Zmieniona", pending.Version), CancellationToken.None);
        var delete = await _service.DeleteAsync(pending.Id, CancellationToken.None);

        Assert.Equal(TodoListErrorType.NotFound, rename.Error!.Type);
        Assert.Equal(TodoListErrorType.NotFound, delete.Error!.Type);
    }

    private sealed class TestUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private sealed class FakeTodoListRepository : ITodoListRepository
    {
        public List<TodoList> Lists { get; } = [];

        /// <summary>Punkty – tylko do liczników w GetSummariesByUserAsync.</summary>
        public List<TodoItem> Items { get; } = [];

        public List<TodoListMember> Members { get; } = [];

        public Task<IReadOnlyList<TodoListSummary>> GetSummariesByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TodoListSummary>>(Lists
                .Where(l => AccessOf(l, userId) is not null)
                .Select(l => new TodoListSummary(
                    l,
                    Items.Count(i => i.ListId == l.Id),
                    Items.Count(i => i.ListId == l.Id && i.IsDone),
                    AccessOf(l, userId)!.Value))
                .ToList());

        public Task<TodoListWithAccess?> GetWithAccessAsync(Guid id, Guid userId, CancellationToken cancellationToken)
        {
            var list = Lists.FirstOrDefault(l => l.Id == id);

            return Task.FromResult(list is not null && AccessOf(list, userId) is { } access
                ? new TodoListWithAccess(list, access)
                : null);
        }

        public Task<IReadOnlyList<Guid>> GetUserIdsWithAccessAsync(Guid id, CancellationToken cancellationToken)
        {
            var list = Lists.FirstOrDefault(l => l.Id == id);

            return Task.FromResult<IReadOnlyList<Guid>>(list is null
                ? []
                : [list.UserId, .. Members.Where(m => m.ListId == id && m.AcceptedAt != null).Select(m => m.UserId)]);
        }

        private TodoListAccess? AccessOf(TodoList list, Guid userId) =>
            list.UserId == userId
                ? TodoListAccess.Owner
                : Members.FirstOrDefault(m => m.ListId == list.Id && m.UserId == userId && m.AcceptedAt != null)?.Role.ToAccess();

        public Task AddAsync(TodoList list, CancellationToken cancellationToken)
        {
            Lists.Add(list);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TodoList list, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(TodoList list, CancellationToken cancellationToken)
        {
            Lists.Remove(list);
            Members.RemoveAll(m => m.ListId == list.Id); // Jak kaskada w bazie.
            return Task.CompletedTask;
        }
    }
}
