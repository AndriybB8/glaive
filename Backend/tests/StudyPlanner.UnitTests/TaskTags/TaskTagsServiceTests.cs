using StudyPlanner.Application.Common;
using StudyPlanner.Application.Tags;
using StudyPlanner.Application.Tasks;
using StudyPlanner.Application.TaskTags;
using StudyPlanner.Domain.Tags;
using StudyPlanner.Domain.Tasks;
using StudyPlanner.Domain.TaskTags;

namespace StudyPlanner.UnitTests.TaskTags;

public class TaskTagServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private readonly FakeTaskRepository _taskRepository = new();
    private readonly FakeTagRepository _tagRepository = new();
    private readonly FakeTaskTagRepository _taskTagRepository;
    private readonly TaskTagService _service;

    private readonly TaskItem _task;
    private readonly Tag _tag;

    public TaskTagServiceTests()
    {
        _taskTagRepository = new FakeTaskTagRepository(_tagRepository.Tags);
        _service = new TaskTagService(_taskTagRepository, _taskRepository, _tagRepository, new TestUser(UserId));

        _task = TaskItem.Create(UserId, Guid.NewGuid(), "Zadanie", null, null, TaskPriority.Medium, TaskItemStatus.ToDo, DateTime.UtcNow);
        _taskRepository.Tasks.Add(_task);

        _tag = Tag.Create(UserId, "Matematyka", "#3B82F6", DateTime.UtcNow);
        _tagRepository.Tags.Add(_tag);
    }

    [Fact]
    public async Task Assign_WithValidTaskAndTag_CreatesAssignment()
    {
        var result = await _service.AssignAsync(_task.Id, _tag.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(await _taskTagRepository.ExistsAsync(_task.Id, _tag.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Assign_WhenAlreadyAssigned_ReturnsSuccessWithoutDuplicate()
    {
        await _service.AssignAsync(_task.Id, _tag.Id, CancellationToken.None);

        var result = await _service.AssignAsync(_task.Id, _tag.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_taskTagRepository.Assignments);
    }

    [Fact]
    public async Task Assign_WithTaskOfAnotherUser_ReturnsNotFound()
    {
        var foreignTask = TaskItem.Create(OtherUserId, Guid.NewGuid(), "Cudze", null, null, TaskPriority.Low, TaskItemStatus.ToDo, DateTime.UtcNow);
        _taskRepository.Tasks.Add(foreignTask);

        var result = await _service.AssignAsync(foreignTask.Id, _tag.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(TaskTagErrorType.NotFound, result.Error!.Type);
        Assert.Empty(_taskTagRepository.Assignments);
    }

    [Fact]
    public async Task Assign_WithTagOfAnotherUser_ReturnsNotFound()
    {
        var foreignTag = Tag.Create(OtherUserId, "Cudzy", "#FF0000", DateTime.UtcNow);
        _tagRepository.Tags.Add(foreignTag);

        var result = await _service.AssignAsync(_task.Id, foreignTag.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(TaskTagErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Assign_WithNonexistentTask_ReturnsNotFound()
    {
        var result = await _service.AssignAsync(Guid.NewGuid(), _tag.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(TaskTagErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Unassign_RemovesAssignment()
    {
        await _service.AssignAsync(_task.Id, _tag.Id, CancellationToken.None);

        var result = await _service.UnassignAsync(_task.Id, _tag.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_taskTagRepository.Assignments);
    }

    [Fact]
    public async Task Unassign_WhenNotAssigned_ReturnsSuccess()
    {
        var result = await _service.UnassignAsync(_task.Id, _tag.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetTagsForTask_ReturnsAssignedTags()
    {
        await _service.AssignAsync(_task.Id, _tag.Id, CancellationToken.None);

        var result = await _service.GetTagsForTaskAsync(_task.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_tag.Id, Assert.Single(result.Value!).Id);
    }

    [Fact]
    public async Task GetTagsForTask_WithTaskOfAnotherUser_ReturnsNotFound()
    {
        var foreignTask = TaskItem.Create(OtherUserId, Guid.NewGuid(), "Cudze", null, null, TaskPriority.Low, TaskItemStatus.ToDo, DateTime.UtcNow);
        _taskRepository.Tasks.Add(foreignTask);

        var result = await _service.GetTagsForTaskAsync(foreignTask.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(TaskTagErrorType.NotFound, result.Error!.Type);
    }

    private sealed class TestUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
    }

    private sealed class FakeTaskRepository : ITaskRepository
    {
        public List<TaskItem> Tasks { get; } = [];

        public Task<IReadOnlyList<TaskItem>> GetByScheduleAsync(Guid scheduleId, Guid userId, IReadOnlyList<Guid> tagIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TaskItem>>(Tasks.Where(t => t.ScheduleId == scheduleId && t.UserId == userId).ToList());

        public Task<TaskItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Tasks.FirstOrDefault(t => t.Id == id && t.UserId == userId));

        public Task AddAsync(TaskItem task, CancellationToken cancellationToken)
        {
            Tasks.Add(task);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TaskItem task, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(TaskItem task, CancellationToken cancellationToken)
        {
            Tasks.Remove(task);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTagRepository : ITagRepository
    {
        public List<Tag> Tags { get; } = [];

        public Task<IReadOnlyList<Tag>> GetByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Tag>>(Tags.Where(t => t.UserId == userId).ToList());

        public Task<Tag?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Tags.FirstOrDefault(t => t.Id == id && t.UserId == userId));

        public Task AddAsync(Tag tag, CancellationToken cancellationToken)
        {
            Tags.Add(tag);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Tag tag, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(Tag tag, CancellationToken cancellationToken)
        {
            Tags.Remove(tag);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTaskTagRepository(List<Tag> tags) : ITaskTagRepository
    {
        public List<TaskTag> Assignments { get; } = [];

        public Task<IReadOnlyList<Tag>> GetTagsForTaskAsync(Guid taskId, CancellationToken cancellationToken)
        {
            var tagIds = Assignments.Where(a => a.TaskId == taskId).Select(a => a.TagId).ToHashSet();
            IReadOnlyList<Tag> result = tags.Where(t => tagIds.Contains(t.Id)).ToList();
            return Task.FromResult(result);
        }

        public Task<bool> ExistsAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken) =>
            Task.FromResult(Assignments.Any(a => a.TaskId == taskId && a.TagId == tagId));

        public Task AssignAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken)
        {
            Assignments.Add(new TaskTag(taskId, tagId));
            return Task.CompletedTask;
        }

        public Task UnassignAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken)
        {
            Assignments.RemoveAll(a => a.TaskId == taskId && a.TagId == tagId);
            return Task.CompletedTask;
        }
    }
}
