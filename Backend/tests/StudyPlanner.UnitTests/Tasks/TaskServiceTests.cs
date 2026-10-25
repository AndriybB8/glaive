using StudyPlanner.Application.Common;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Application.Tasks;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.UnitTests.Tasks;

public class TaskServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Jan25 = new(2027, 1, 25);
    private static readonly DateOnly Feb1 = new(2027, 2, 1);
    private static readonly DateOnly Feb14 = new(2027, 2, 14);

    private readonly FakeTaskRepository _tasks = new();
    private readonly FakeScheduleRepository _schedules = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
    private readonly TaskService _service;
    private readonly Schedule _schedule;

    public TaskServiceTests()
    {
        _service = new TaskService(_tasks, _schedules, new TestUser(UserId), _time);
        _schedule = Schedule.Create(UserId, "Sesja zimowa", Jan25, Feb14, DateTime.UtcNow);
        _schedules.Items.Add(_schedule);
    }

    private async Task<TaskDto> CreateAsync(string title = "Powtórka", DateOnly? dueDate = null) =>
        (await _service.CreateAsync(_schedule.Id, new CreateTaskRequest(title, null, dueDate, null, null), CancellationToken.None)).Value!;

    [Fact]
    public async Task Create_UsesDefaultsAndBelongsToSchedule()
    {
        var result = await _service.CreateAsync(_schedule.Id, new CreateTaskRequest("  Algebra – rozdział 3  ", null, Feb1, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var task = result.Value!;
        Assert.Equal("Algebra – rozdział 3", task.Title);
        Assert.Equal(_schedule.Id, task.ScheduleId);
        Assert.Equal(Feb1, task.DueDate);
        Assert.Equal(TaskPriority.Medium, task.Priority);
        Assert.Equal(TaskItemStatus.ToDo, task.Status);
        Assert.Equal(1, task.Version);
        Assert.Equal(UserId, _tasks.Items.Single().UserId);
    }

    [Fact]
    public async Task Create_AllowsNoDueDateAndDueDateOnRangeEdges()
    {
        var noDate = await _service.CreateAsync(_schedule.Id, new CreateTaskRequest("A", null, null, TaskPriority.High, TaskItemStatus.InProgress), CancellationToken.None);
        var first = await _service.CreateAsync(_schedule.Id, new CreateTaskRequest("B", null, Jan25, null, null), CancellationToken.None);
        var last = await _service.CreateAsync(_schedule.Id, new CreateTaskRequest("C", null, Feb14, null, null), CancellationToken.None);

        Assert.True(noDate.IsSuccess);
        Assert.True(first.IsSuccess);
        Assert.True(last.IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RejectsMissingTitle(string? title)
    {
        var result = await _service.CreateAsync(_schedule.Id, new CreateTaskRequest(title, null, null, null, null), CancellationToken.None);

        Assert.Equal(TaskErrorType.Validation, result.Error!.Type);
        Assert.Empty(_tasks.Items);
    }

    [Fact]
    public async Task Create_RejectsDueDateOutsideSchedule()
    {
        var before = await _service.CreateAsync(_schedule.Id, new CreateTaskRequest("A", null, Jan25.AddDays(-1), null, null), CancellationToken.None);
        var after = await _service.CreateAsync(_schedule.Id, new CreateTaskRequest("A", null, Feb14.AddDays(1), null, null), CancellationToken.None);

        Assert.Equal(TaskErrorType.Validation, before.Error!.Type);
        Assert.Equal(TaskErrorType.Validation, after.Error!.Type);
        Assert.Empty(_tasks.Items);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  Rozdziały 1–3, zadania z list.  ", "Rozdziały 1–3, zadania z list.")]
    public async Task Create_TrimsDescriptionAndTreatsEmptyAsNone(string? description, string? expected)
    {
        var result = await _service.CreateAsync(_schedule.Id, new CreateTaskRequest("A", description, null, null, null), CancellationToken.None);

        Assert.Equal(expected, result.Value!.Description);
    }

    [Fact]
    public async Task Create_RejectsTooLongDescription()
    {
        var description = new string('a', TaskItem.DescriptionMaxLength + 1);

        var result = await _service.CreateAsync(_schedule.Id, new CreateTaskRequest("A", description, null, null, null), CancellationToken.None);

        Assert.Equal(TaskErrorType.Validation, result.Error!.Type);
        Assert.Empty(_tasks.Items);
    }

    [Fact]
    public async Task Update_ChangesAndClearsDescription()
    {
        var created = await CreateAsync();

        var added = await _service.UpdateAsync(created.Id, new UpdateTaskRequest("Powtórka", "Notatki z wykładu 3", null, null, null, 1), CancellationToken.None);
        var cleared = await _service.UpdateAsync(created.Id, new UpdateTaskRequest("Powtórka", "", null, null, null, 2), CancellationToken.None);

        Assert.Equal("Notatki z wykładu 3", added.Value!.Description);
        Assert.Equal(2, added.Value.Version);
        Assert.Null(cleared.Value!.Description);
        Assert.Equal(3, cleared.Value.Version);
    }

    [Fact]
    public async Task Create_RejectsUndefinedPriority()
    {
        var result = await _service.CreateAsync(_schedule.Id, new CreateTaskRequest("A", null, null, (TaskPriority)42, null), CancellationToken.None);

        Assert.Equal(TaskErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Create_InUnknownSchedule_IsNotFound()
    {
        var result = await _service.CreateAsync(Guid.NewGuid(), new CreateTaskRequest("A", null, null, null, null), CancellationToken.None);

        Assert.Equal(TaskErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task GetBySchedule_SortsByDueDateThenPriority()
    {
        await _service.CreateAsync(_schedule.Id, new CreateTaskRequest("Bez terminu", null, null, TaskPriority.High, null), CancellationToken.None);
        await _service.CreateAsync(_schedule.Id, new CreateTaskRequest("Później", null, Feb14, null, null), CancellationToken.None);
        await _service.CreateAsync(_schedule.Id, new CreateTaskRequest("Wcześniej, niski", null, Feb1, TaskPriority.Low, null), CancellationToken.None);
        await _service.CreateAsync(_schedule.Id, new CreateTaskRequest("Wcześniej, wysoki", null, Feb1, TaskPriority.High, null), CancellationToken.None);

        var result = await _service.GetByScheduleAsync(_schedule.Id, CancellationToken.None);

        Assert.Equal(["Wcześniej, wysoki", "Wcześniej, niski", "Później", "Bez terminu"], result.Value!.Select(t => t.Title));
    }

    [Fact]
    public async Task GetBySchedule_ReturnsOnlyThatSchedulesTasks()
    {
        var other = Schedule.Create(UserId, "Inny", Jan25, Feb14, DateTime.UtcNow);
        _schedules.Items.Add(other);
        await CreateAsync("Moje");
        await _service.CreateAsync(other.Id, new CreateTaskRequest("Z innego", null, null, null, null), CancellationToken.None);

        var result = await _service.GetByScheduleAsync(_schedule.Id, CancellationToken.None);

        Assert.Equal("Moje", Assert.Single(result.Value!).Title);
    }

    [Fact]
    public async Task Update_WithCurrentVersion_SavesAndIncrementsVersion()
    {
        var created = await CreateAsync();
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.UpdateAsync(
            created.Id,
            new UpdateTaskRequest("Powtórka całości", null, Feb1, TaskPriority.High, TaskItemStatus.InProgress, created.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Powtórka całości", result.Value!.Title);
        Assert.Equal(Feb1, result.Value.DueDate);
        Assert.Equal(TaskPriority.High, result.Value.Priority);
        Assert.Equal(TaskItemStatus.InProgress, result.Value.Status);
        Assert.Equal(2, result.Value.Version);
        Assert.True(result.Value.UpdatedAt > created.UpdatedAt);
    }

    [Fact]
    public async Task Update_WithNullDueDate_RemovesDueDate()
    {
        var created = await CreateAsync(dueDate: Feb1);

        var result = await _service.UpdateAsync(created.Id, new UpdateTaskRequest("Powtórka", null, null, null, null, 1), CancellationToken.None);

        Assert.Null(result.Value!.DueDate);
        Assert.Equal(TaskPriority.Medium, result.Value.Priority);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ReturnsConflictWithCurrentTask()
    {
        var created = await CreateAsync();
        await _service.UpdateAsync(created.Id, new UpdateTaskRequest("Z innej karty", null, null, null, null, 1), CancellationToken.None);

        var result = await _service.UpdateAsync(created.Id, new UpdateTaskRequest("Stara wersja", null, null, null, null, 1), CancellationToken.None);

        Assert.Equal(TaskErrorType.Conflict, result.Error!.Type);
        Assert.Equal("Z innej karty", result.Error.Current!.Title);
    }

    [Fact]
    public async Task Update_WithoutChanges_DoesNotIncrementVersion()
    {
        var created = await CreateAsync(dueDate: Feb1);

        var result = await _service.UpdateAsync(created.Id, new UpdateTaskRequest(" Powtórka ", null, Feb1, TaskPriority.Medium, TaskItemStatus.ToDo, 1), CancellationToken.None);

        Assert.Equal(1, result.Value!.Version);
    }

    [Fact]
    public async Task ChangeStatus_UpdatesOnlyStatus()
    {
        var created = await CreateAsync(dueDate: Feb1);

        var result = await _service.ChangeStatusAsync(created.Id, new ChangeTaskStatusRequest(TaskItemStatus.Done), CancellationToken.None);

        Assert.Equal(TaskItemStatus.Done, result.Value!.Status);
        Assert.Equal(Feb1, result.Value.DueDate);
        Assert.Equal(2, result.Value.Version);
    }

    [Fact]
    public async Task ChangeStatus_RejectsMissingStatus()
    {
        var created = await CreateAsync();

        var result = await _service.ChangeStatusAsync(created.Id, new ChangeTaskStatusRequest(null), CancellationToken.None);

        Assert.Equal(TaskErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Delete_RemovesTask()
    {
        var created = await CreateAsync();

        var result = await _service.DeleteAsync(created.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_tasks.Items);
    }

    [Fact]
    public async Task TaskOfDeletedSchedule_IsNotFound()
    {
        var created = await CreateAsync();
        _schedules.Items.Clear();

        var get = await _service.GetByIdAsync(created.Id, CancellationToken.None);
        var status = await _service.ChangeStatusAsync(created.Id, new ChangeTaskStatusRequest(TaskItemStatus.Done), CancellationToken.None);
        var list = await _service.GetByScheduleAsync(_schedule.Id, CancellationToken.None);

        Assert.Equal(TaskErrorType.NotFound, get.Error!.Type);
        Assert.Equal(TaskErrorType.NotFound, status.Error!.Type);
        Assert.Equal(TaskErrorType.NotFound, list.Error!.Type);
    }

    [Fact]
    public async Task TaskOfAnotherUser_IsNotFound()
    {
        var foreign = TaskItem.Create(Guid.NewGuid(), _schedule.Id, "Cudze", null, null, TaskPriority.Low, TaskItemStatus.ToDo, DateTime.UtcNow);
        _tasks.Items.Add(foreign);

        var get = await _service.GetByIdAsync(foreign.Id, CancellationToken.None);
        var update = await _service.UpdateAsync(foreign.Id, new UpdateTaskRequest("Moje", null, null, null, null, 1), CancellationToken.None);
        var delete = await _service.DeleteAsync(foreign.Id, CancellationToken.None);

        Assert.Equal(TaskErrorType.NotFound, get.Error!.Type);
        Assert.Equal(TaskErrorType.NotFound, update.Error!.Type);
        Assert.Equal(TaskErrorType.NotFound, delete.Error!.Type);
        Assert.Equal("Cudze", _tasks.Items.Single().Title);
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

    private sealed class FakeTaskRepository : ITaskRepository
    {
        public List<TaskItem> Items { get; } = [];

        public Task<IReadOnlyList<TaskItem>> GetByScheduleAsync(Guid scheduleId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TaskItem>>(Items.Where(t => t.ScheduleId == scheduleId && t.UserId == userId).ToList());

        public Task<TaskItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(t => t.Id == id && t.UserId == userId));

        public Task AddAsync(TaskItem task, CancellationToken cancellationToken)
        {
            Items.Add(task);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TaskItem task, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(TaskItem task, CancellationToken cancellationToken)
        {
            Items.Remove(task);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public List<Schedule> Items { get; } = [];

        public Task<IReadOnlyList<Schedule>> GetByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Schedule>>(Items.Where(s => s.UserId == userId).ToList());

        public Task<Schedule?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(s => s.Id == id && s.UserId == userId));

        public Task AddAsync(Schedule schedule, CancellationToken cancellationToken)
        {
            Items.Add(schedule);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Schedule schedule, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(Schedule schedule, CancellationToken cancellationToken)
        {
            Items.Remove(schedule);
            return Task.CompletedTask;
        }
    }
}
