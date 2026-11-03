using StudyPlanner.Application.Common;
using StudyPlanner.Application.TodoLists;
using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.UnitTests.TodoLists;

public class TodoListServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly FakeTodoListRepository _repository = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly TodoListService _service;

    public TodoListServiceTests()
    {
        _service = new TodoListService(_repository, new TestUser(UserId), _time);
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

        public Task<IReadOnlyList<TodoListSummary>> GetSummariesByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TodoListSummary>>(Lists
                .Where(l => l.UserId == userId)
                .Select(l => new TodoListSummary(
                    l,
                    Items.Count(i => i.ListId == l.Id),
                    Items.Count(i => i.ListId == l.Id && i.IsDone)))
                .ToList());

        public Task<TodoList?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Lists.FirstOrDefault(l => l.Id == id && l.UserId == userId));

        public Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Lists.Any(l => l.Id == id && l.UserId == userId));

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
