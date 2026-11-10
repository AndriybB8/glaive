using StudyPlanner.Application.Common;
using StudyPlanner.Application.TodoLists;
using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.UnitTests.TodoLists;

public class TodoItemServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly FakeTodoItemRepository _items = new();
    private readonly FakeTodoListRepository _lists = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly TodoItemService _service;
    private readonly TodoList _list;

    public TodoItemServiceTests()
    {
        _service = new TodoItemService(_items, _lists, new TestUser(UserId), _time);
        _list = TodoList.Create(UserId, "Na egzamin", DateTime.UtcNow);
        _lists.Lists.Add(_list);
    }

    private async Task<TodoItemDto> CreateAsync(string title = "Powtórzyć SQL") =>
        (await _service.CreateAsync(_list.Id, new CreateTodoItemRequest(title), CancellationToken.None)).Value!;

    [Fact]
    public async Task Create_TrimsTitleAndStartsNotDoneInList()
    {
        var result = await _service.CreateAsync(_list.Id, new CreateTodoItemRequest("  Powtórzyć SQL  "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = result.Value!;
        Assert.Equal("Powtórzyć SQL", item.Title);
        Assert.Equal(_list.Id, item.ListId);
        Assert.False(item.IsDone);
        Assert.Null(item.CompletedAt);
        Assert.Equal(0, item.Position);
        Assert.Equal(1, item.Version);
        Assert.Equal(UserId, _items.Items.Single().UserId);
    }

    [Fact]
    public async Task Create_PutsNewItemAtTheEnd()
    {
        await CreateAsync("A");
        var second = await CreateAsync("B");
        await _service.DeleteAsync(second.Id, CancellationToken.None);

        var third = await CreateAsync("C");

        Assert.Equal(1, third.Position);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Create_RejectsEmptyTitle(string? title)
    {
        var result = await _service.CreateAsync(_list.Id, new CreateTodoItemRequest(title), CancellationToken.None);

        Assert.Equal(TodoItemErrorType.Validation, result.Error!.Type);
        Assert.Equal("Podaj treść punktu.", result.Error.Message);
        Assert.Empty(_items.Items);
    }

    [Fact]
    public async Task Create_RejectsTooLongTitle()
    {
        var title = new string('a', TodoItem.TitleMaxLength + 1);

        var result = await _service.CreateAsync(_list.Id, new CreateTodoItemRequest(title), CancellationToken.None);

        Assert.Equal(TodoItemErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Create_InListOfAnotherUser_ReturnsListNotFound()
    {
        var foreign = TodoList.Create(Guid.NewGuid(), "Cudza lista", DateTime.UtcNow);
        _lists.Lists.Add(foreign);

        var result = await _service.CreateAsync(foreign.Id, new CreateTodoItemRequest("Punkt"), CancellationToken.None);

        Assert.Equal(TodoItemErrorType.NotFound, result.Error!.Type);
        Assert.Equal("Nie znaleziono listy.", result.Error.Message);
        Assert.Empty(_items.Items);
    }

    [Fact]
    public async Task GetByList_ReturnsItemsInPositionOrder()
    {
        _items.Items.Add(TodoItem.Create(UserId, _list.Id, "Drugi", 1, DateTime.UtcNow));
        _items.Items.Add(TodoItem.Create(UserId, _list.Id, "Pierwszy", 0, DateTime.UtcNow));
        _items.Items.Add(TodoItem.Create(UserId, Guid.NewGuid(), "Z innej listy", 0, DateTime.UtcNow));

        var result = await _service.GetByListAsync(_list.Id, CancellationToken.None);

        Assert.Equal(["Pierwszy", "Drugi"], result.Value!.Select(i => i.Title));
    }

    [Fact]
    public async Task GetByList_ForMissingList_ReturnsNotFound()
    {
        var result = await _service.GetByListAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(TodoItemErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Update_ChangesTitleAndDoneWithOneVersionBump()
    {
        var created = await CreateAsync();
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.UpdateAsync(created.Id, new UpdateTodoItemRequest("Powtórzyć JOIN-y", true, created.Version), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Powtórzyć JOIN-y", result.Value!.Title);
        Assert.True(result.Value.IsDone);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, result.Value.CompletedAt);
        Assert.Equal(2, result.Value.Version);
    }

    [Fact]
    public async Task Update_WithoutIsDone_KeepsCurrentState()
    {
        var created = await CreateAsync();
        await _service.SetDoneAsync(created.Id, new SetTodoItemDoneRequest(true), CancellationToken.None);

        var result = await _service.UpdateAsync(created.Id, new UpdateTodoItemRequest("Nowa treść", null, 2), CancellationToken.None);

        Assert.True(result.Value!.IsDone);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ReturnsConflictWithCurrentItem()
    {
        var created = await CreateAsync("A");
        await _service.UpdateAsync(created.Id, new UpdateTodoItemRequest("B", null, created.Version), CancellationToken.None);

        var result = await _service.UpdateAsync(created.Id, new UpdateTodoItemRequest("C", null, created.Version), CancellationToken.None);

        Assert.Equal(TodoItemErrorType.Conflict, result.Error!.Type);
        Assert.Equal("B", result.Error.Current!.Title);
    }

    [Fact]
    public async Task Update_WithoutChanges_DoesNotIncrementVersion()
    {
        var created = await CreateAsync("A");

        var result = await _service.UpdateAsync(created.Id, new UpdateTodoItemRequest("A", false, created.Version), CancellationToken.None);

        Assert.Equal(1, result.Value!.Version);
    }

    [Fact]
    public async Task Update_RejectsEmptyTitle()
    {
        var created = await CreateAsync("A");

        var result = await _service.UpdateAsync(created.Id, new UpdateTodoItemRequest("", null, created.Version), CancellationToken.None);

        Assert.Equal(TodoItemErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task SetDone_TogglesWithoutVersionCheck()
    {
        var created = await CreateAsync();

        var checkedResult = await _service.SetDoneAsync(created.Id, new SetTodoItemDoneRequest(true), CancellationToken.None);
        var uncheckedResult = await _service.SetDoneAsync(created.Id, new SetTodoItemDoneRequest(false), CancellationToken.None);

        Assert.True(checkedResult.Value!.IsDone);
        Assert.NotNull(checkedResult.Value.CompletedAt);
        Assert.False(uncheckedResult.Value!.IsDone);
        Assert.Null(uncheckedResult.Value.CompletedAt);
        Assert.Equal(3, uncheckedResult.Value.Version);
    }

    [Fact]
    public async Task SetDone_WithSameValue_DoesNotIncrementVersion()
    {
        var created = await CreateAsync();

        var result = await _service.SetDoneAsync(created.Id, new SetTodoItemDoneRequest(false), CancellationToken.None);

        Assert.Equal(1, result.Value!.Version);
    }

    [Fact]
    public async Task SetDone_RequiresValue()
    {
        var created = await CreateAsync();

        var result = await _service.SetDoneAsync(created.Id, new SetTodoItemDoneRequest(null), CancellationToken.None);

        Assert.Equal(TodoItemErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task ItemOfAnotherUser_IsNotFoundForEveryOperation()
    {
        var foreign = TodoItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Cudzy punkt", 0, DateTime.UtcNow);
        _items.Items.Add(foreign);

        var get = await _service.GetByIdAsync(foreign.Id, CancellationToken.None);
        var update = await _service.UpdateAsync(foreign.Id, new UpdateTodoItemRequest("X", true, 1), CancellationToken.None);
        var setDone = await _service.SetDoneAsync(foreign.Id, new SetTodoItemDoneRequest(true), CancellationToken.None);
        var delete = await _service.DeleteAsync(foreign.Id, CancellationToken.None);

        Assert.Equal(TodoItemErrorType.NotFound, get.Error!.Type);
        Assert.Equal(TodoItemErrorType.NotFound, update.Error!.Type);
        Assert.Equal(TodoItemErrorType.NotFound, setDone.Error!.Type);
        Assert.Equal(TodoItemErrorType.NotFound, delete.Error!.Type);
        Assert.False(foreign.IsDone);
        Assert.Single(_items.Items);
    }

    [Fact]
    public async Task Delete_RemovesOwnItem()
    {
        var created = await CreateAsync();

        var result = await _service.DeleteAsync(created.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_items.Items);
    }

    [Fact]
    public async Task Reorder_SetsPositionsByOrderAndReturnsItemsInNewOrder()
    {
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");
        var c = await CreateAsync("C");

        var result = await _service.ReorderAsync(_list.Id, new ReorderTodoItemsRequest([c.Id, a.Id, b.Id]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["C", "A", "B"], result.Value!.Select(i => i.Title));
        Assert.Equal([0, 1, 2], result.Value!.Select(i => i.Position));
        var list = (await _service.GetByListAsync(_list.Id, CancellationToken.None)).Value!;
        Assert.Equal(["C", "A", "B"], list.Select(i => i.Title));
    }

    [Fact]
    public async Task Reorder_ChangesVersionOnlyOfMovedItems()
    {
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");
        var c = await CreateAsync("C");
        _time.Advance(TimeSpan.FromMinutes(1));

        // Zamiana B i C – A zostaje na miejscu.
        var result = await _service.ReorderAsync(_list.Id, new ReorderTodoItemsRequest([a.Id, c.Id, b.Id]), CancellationToken.None);

        var byTitle = result.Value!.ToDictionary(i => i.Title);
        Assert.Equal(1, byTitle["A"].Version);
        Assert.Equal(a.UpdatedAt, byTitle["A"].UpdatedAt);
        Assert.Equal(2, byTitle["B"].Version);
        Assert.Equal(2, byTitle["C"].Version);
        Assert.True(byTitle["C"].UpdatedAt > c.UpdatedAt);
    }

    [Fact]
    public async Task Reorder_InSameOrder_ChangesNothing()
    {
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");

        var result = await _service.ReorderAsync(_list.Id, new ReorderTodoItemsRequest([a.Id, b.Id]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!, i => Assert.Equal(1, i.Version));
        Assert.Equal(0, _items.RangeUpdates);
    }

    [Fact]
    public async Task Reorder_WithMissingItem_IsConflict()
    {
        var a = await CreateAsync("A");
        await CreateAsync("B");

        var result = await _service.ReorderAsync(_list.Id, new ReorderTodoItemsRequest([a.Id]), CancellationToken.None);

        Assert.Equal(TodoItemErrorType.Conflict, result.Error!.Type);
        Assert.All(_items.Items, i => Assert.Equal(1, i.Version));
    }

    [Fact]
    public async Task Reorder_WithItemFromAnotherList_IsConflict()
    {
        var a = await CreateAsync("A");
        var otherList = TodoList.Create(UserId, "Inna", DateTime.UtcNow);
        _lists.Lists.Add(otherList);
        var foreign = (await _service.CreateAsync(otherList.Id, new CreateTodoItemRequest("Z innej listy"), CancellationToken.None)).Value!;

        var result = await _service.ReorderAsync(_list.Id, new ReorderTodoItemsRequest([foreign.Id, a.Id]), CancellationToken.None);

        Assert.Equal(TodoItemErrorType.Conflict, result.Error!.Type);
        Assert.Equal(0, _items.Items.Single(i => i.Id == foreign.Id).Position);
    }

    [Fact]
    public async Task Reorder_WithDuplicateOrMissingIds_IsValidation()
    {
        var a = await CreateAsync("A");
        await CreateAsync("B");

        var duplicate = await _service.ReorderAsync(_list.Id, new ReorderTodoItemsRequest([a.Id, a.Id]), CancellationToken.None);
        var missing = await _service.ReorderAsync(_list.Id, new ReorderTodoItemsRequest(null), CancellationToken.None);

        Assert.Equal(TodoItemErrorType.Validation, duplicate.Error!.Type);
        Assert.Equal(TodoItemErrorType.Validation, missing.Error!.Type);
    }

    [Fact]
    public async Task Reorder_ListOfAnotherUser_IsNotFound()
    {
        var foreignList = TodoList.Create(Guid.NewGuid(), "Cudza", DateTime.UtcNow);
        _lists.Lists.Add(foreignList);

        var result = await _service.ReorderAsync(foreignList.Id, new ReorderTodoItemsRequest([]), CancellationToken.None);

        Assert.Equal(TodoItemErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Reorder_WhenSaveConflicts_IsConflict()
    {
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");
        _items.FailNextRangeUpdate = true;

        var result = await _service.ReorderAsync(_list.Id, new ReorderTodoItemsRequest([b.Id, a.Id]), CancellationToken.None);

        Assert.Equal(TodoItemErrorType.Conflict, result.Error!.Type);
    }

    /// <summary>Lista innej osoby z jednym punktem, udostępniona bieżącemu użytkownikowi.</summary>
    private (TodoList List, TodoItem Item) SharedList(TodoListMemberRole role, bool accepted = true)
    {
        var ownerId = Guid.NewGuid();
        var list = TodoList.Create(ownerId, "Cudza lista", DateTime.UtcNow);
        _lists.Lists.Add(list);

        var item = TodoItem.Create(ownerId, list.Id, "Punkt właściciela", 0, DateTime.UtcNow);
        _items.Items.Add(item);

        var member = TodoListMember.Invite(list.Id, UserId, role, DateTime.UtcNow);
        if (accepted)
        {
            member.Accept(DateTime.UtcNow);
        }

        _lists.Members.Add(member);
        return (list, item);
    }

    [Theory]
    [InlineData(TodoListMemberRole.Viewer)]
    [InlineData(TodoListMemberRole.Editor)]
    public async Task SharedList_CanBeReadByAcceptedMember(TodoListMemberRole role)
    {
        var (list, item) = SharedList(role);

        var byList = await _service.GetByListAsync(list.Id, CancellationToken.None);
        var byId = await _service.GetByIdAsync(item.Id, CancellationToken.None);

        Assert.Equal(item.Id, byList.Value!.Single().Id);
        Assert.Equal(item.Id, byId.Value!.Id);
    }

    [Fact]
    public async Task SharedList_ViewerCannotChangeAnything()
    {
        var (list, item) = SharedList(TodoListMemberRole.Viewer);

        var create = await _service.CreateAsync(list.Id, new CreateTodoItemRequest("Nowy"), CancellationToken.None);
        var update = await _service.UpdateAsync(item.Id, new UpdateTodoItemRequest("Zmieniony", true, item.Version), CancellationToken.None);
        var setDone = await _service.SetDoneAsync(item.Id, new SetTodoItemDoneRequest(true), CancellationToken.None);
        var reorder = await _service.ReorderAsync(list.Id, new ReorderTodoItemsRequest([item.Id]), CancellationToken.None);
        var delete = await _service.DeleteAsync(item.Id, CancellationToken.None);

        Assert.Equal(TodoItemErrorType.Forbidden, create.Error!.Type);
        Assert.Equal(TodoItemErrorType.Forbidden, update.Error!.Type);
        Assert.Equal(TodoItemErrorType.Forbidden, setDone.Error!.Type);
        Assert.Equal(TodoItemErrorType.Forbidden, reorder.Error!.Type);
        Assert.Equal(TodoItemErrorType.Forbidden, delete.Error!.Type);
        Assert.Equal("Punkt właściciela", _items.Items.Single().Title);
        Assert.False(item.IsDone);
        Assert.Equal(1, item.Version);
    }

    [Fact]
    public async Task SharedList_EditorCanChangeItems()
    {
        var (list, item) = SharedList(TodoListMemberRole.Editor);

        var create = await _service.CreateAsync(list.Id, new CreateTodoItemRequest("Nowy"), CancellationToken.None);
        var update = await _service.UpdateAsync(item.Id, new UpdateTodoItemRequest("Zmieniony", null, item.Version), CancellationToken.None);
        var setDone = await _service.SetDoneAsync(item.Id, new SetTodoItemDoneRequest(true), CancellationToken.None);
        var reorder = await _service.ReorderAsync(list.Id, new ReorderTodoItemsRequest([create.Value!.Id, item.Id]), CancellationToken.None);
        var delete = await _service.DeleteAsync(item.Id, CancellationToken.None);

        Assert.True(create.IsSuccess);
        Assert.True(update.IsSuccess);
        Assert.True(setDone.IsSuccess);
        Assert.Equal(["Nowy", "Zmieniony"], reorder.Value!.Select(i => i.Title));
        Assert.True(delete.IsSuccess);
        Assert.Equal("Nowy", _items.Items.Single().Title);
    }

    [Fact]
    public async Task SharedList_ItemAddedByEditorBelongsToListOwner()
    {
        var (list, _) = SharedList(TodoListMemberRole.Editor);

        var created = await _service.CreateAsync(list.Id, new CreateTodoItemRequest("Od edytora"), CancellationToken.None);

        Assert.Equal(list.UserId, _items.Items.Single(i => i.Id == created.Value!.Id).UserId);
    }

    [Fact]
    public async Task SharedList_WithPendingInvitation_IsNotFoundForEveryOperation()
    {
        var (list, item) = SharedList(TodoListMemberRole.Editor, accepted: false);

        var byList = await _service.GetByListAsync(list.Id, CancellationToken.None);
        var byId = await _service.GetByIdAsync(item.Id, CancellationToken.None);
        var create = await _service.CreateAsync(list.Id, new CreateTodoItemRequest("Nowy"), CancellationToken.None);
        var update = await _service.UpdateAsync(item.Id, new UpdateTodoItemRequest("Zmieniony", null, item.Version), CancellationToken.None);
        var setDone = await _service.SetDoneAsync(item.Id, new SetTodoItemDoneRequest(true), CancellationToken.None);
        var reorder = await _service.ReorderAsync(list.Id, new ReorderTodoItemsRequest([item.Id]), CancellationToken.None);
        var delete = await _service.DeleteAsync(item.Id, CancellationToken.None);

        Assert.Equal(TodoItemErrorType.NotFound, byList.Error!.Type);
        Assert.Equal(TodoItemErrorType.NotFound, byId.Error!.Type);
        Assert.Equal(TodoItemErrorType.NotFound, create.Error!.Type);
        Assert.Equal(TodoItemErrorType.NotFound, update.Error!.Type);
        Assert.Equal(TodoItemErrorType.NotFound, setDone.Error!.Type);
        Assert.Equal(TodoItemErrorType.NotFound, reorder.Error!.Type);
        Assert.Equal(TodoItemErrorType.NotFound, delete.Error!.Type);
        Assert.Single(_items.Items);
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

    private sealed class FakeTodoItemRepository : ITodoItemRepository
    {
        public List<TodoItem> Items { get; } = [];

        public Task<IReadOnlyList<TodoItem>> GetByListAsync(Guid listId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TodoItem>>(Items.Where(i => i.ListId == listId).ToList());

        public Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(i => i.Id == id));

        public Task<IReadOnlyList<TodoItem>> GetByListForUpdateAsync(Guid listId, CancellationToken cancellationToken) =>
            GetByListAsync(listId, cancellationToken);

        /// <summary>Ile razy zapisano kilka punktów naraz.</summary>
        public int RangeUpdates { get; private set; }

        /// <summary>Następny zapis kilku punktów zgłosi konflikt wersji (jak przy zmianie w innej karcie).</summary>
        public bool FailNextRangeUpdate { get; set; }

        public Task UpdateRangeAsync(IReadOnlyList<TodoItem> items, CancellationToken cancellationToken)
        {
            if (FailNextRangeUpdate)
            {
                FailNextRangeUpdate = false;
                throw new TodoItemConcurrencyException();
            }

            RangeUpdates++;
            return Task.CompletedTask;
        }

        public Task AddAsync(TodoItem item, CancellationToken cancellationToken)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TodoItem item, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(TodoItem item, CancellationToken cancellationToken)
        {
            Items.Remove(item);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTodoListRepository : ITodoListRepository
    {
        public List<TodoList> Lists { get; } = [];

        public List<TodoListMember> Members { get; } = [];

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

        public Task AddAsync(TodoList list, CancellationToken cancellationToken)
        {
            Lists.Add(list);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TodoList list, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(TodoList list, CancellationToken cancellationToken)
        {
            Lists.Remove(list);
            return Task.CompletedTask;
        }
    }
}
