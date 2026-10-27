using StudyPlanner.Application.Common;
using StudyPlanner.Application.Notes;
using StudyPlanner.Application.NoteTasks;
using StudyPlanner.Domain.NoteTasks;
using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.UnitTests.NoteTasks;

public class NoteTaskServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid NoteId = Guid.NewGuid();
    private static readonly Guid TaskId = Guid.NewGuid();

    private readonly FakeNoteTaskRepository _repository = new();
    private readonly NoteTaskService _service;

    public NoteTaskServiceTests()
    {
        _repository.Notes.Add((NoteId, UserId, "Analiza – granice"));
        _repository.Tasks.Add((new LinkedTaskDto(TaskId, Guid.NewGuid(), "Sesja zimowa", "Powtórzyć granice", TaskItemStatus.ToDo, null), UserId));

        var time = new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        _service = new NoteTaskService(_repository, new TestUser(UserId), time);
    }

    [Fact]
    public async Task Link_CreatesLinkWithUtcDateAndReturnsTask()
    {
        var result = await _service.LinkAsync(NoteId, TaskId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Powtórzyć granice", result.Value!.Title);
        Assert.Equal("Sesja zimowa", result.Value.ScheduleName);
        var link = Assert.Single(_repository.Links);
        Assert.Equal(DateTimeKind.Utc, link.CreatedAt.Kind);
    }

    [Fact]
    public async Task Link_Twice_KeepsSingleLink()
    {
        await _service.LinkAsync(NoteId, TaskId, CancellationToken.None);
        var second = await _service.LinkAsync(NoteId, TaskId, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Single(_repository.Links);
    }

    [Fact]
    public async Task Link_ToTaskOfAnotherUser_IsNotFound()
    {
        var foreignTaskId = Guid.NewGuid();
        _repository.Tasks.Add((new LinkedTaskDto(foreignTaskId, Guid.NewGuid(), "Cudzy", "Cudze zadanie", TaskItemStatus.ToDo, null), Guid.NewGuid()));

        var result = await _service.LinkAsync(NoteId, foreignTaskId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(NoteTaskErrorType.NotFound, result.Error!.Type);
        Assert.Empty(_repository.Links);
    }

    [Fact]
    public async Task Link_NoteOfAnotherUser_IsNotFound()
    {
        var foreignNoteId = Guid.NewGuid();
        _repository.Notes.Add((foreignNoteId, Guid.NewGuid(), "Cudza"));

        var result = await _service.LinkAsync(foreignNoteId, TaskId, CancellationToken.None);

        Assert.Equal(NoteTaskErrorType.NotFound, result.Error!.Type);
        Assert.Empty(_repository.Links);
    }

    [Fact]
    public async Task Link_WhenTaskDeletedMeanwhile_IsNotFound()
    {
        _repository.FailNextAdd = true;

        var result = await _service.LinkAsync(NoteId, TaskId, CancellationToken.None);

        Assert.Equal(NoteTaskErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Unlink_RemovesLink_AndIsSafeToRepeat()
    {
        await _service.LinkAsync(NoteId, TaskId, CancellationToken.None);

        var first = await _service.UnlinkAsync(NoteId, TaskId, CancellationToken.None);
        var second = await _service.UnlinkAsync(NoteId, TaskId, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Empty(_repository.Links);
    }

    [Fact]
    public async Task GetTasksForNote_ReturnsLinkedTasks_AndNotFoundForMissingNote()
    {
        await _service.LinkAsync(NoteId, TaskId, CancellationToken.None);

        var tasks = await _service.GetTasksForNoteAsync(NoteId, CancellationToken.None);
        var missing = await _service.GetTasksForNoteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(TaskId, Assert.Single(tasks.Value!).Id);
        Assert.Equal(NoteTaskErrorType.NotFound, missing.Error!.Type);
    }

    [Fact]
    public async Task GetNotesForTask_ReturnsLinkedNotes_AndNotFoundForMissingTask()
    {
        await _service.LinkAsync(NoteId, TaskId, CancellationToken.None);

        var notes = await _service.GetNotesForTaskAsync(TaskId, CancellationToken.None);
        var missing = await _service.GetNotesForTaskAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("Analiza – granice", Assert.Single(notes.Value!).Title);
        Assert.Equal(NoteTaskErrorType.NotFound, missing.Error!.Type);
    }

    private sealed class TestUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeNoteTaskRepository : INoteTaskRepository
    {
        public List<(Guid Id, Guid UserId, string Title)> Notes { get; } = [];

        public List<(LinkedTaskDto Task, Guid UserId)> Tasks { get; } = [];

        public List<NoteTaskLink> Links { get; } = [];

        public bool FailNextAdd { get; set; }

        public Task<bool> NoteExistsAsync(Guid noteId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Notes.Any(n => n.Id == noteId && n.UserId == userId));

        public Task<LinkedTaskDto?> FindTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<LinkedTaskDto?>(Tasks.Where(t => t.Task.Id == taskId && t.UserId == userId).Select(t => t.Task).FirstOrDefault());

        public Task<IReadOnlyList<LinkedTaskDto>> GetTasksForNoteAsync(Guid noteId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LinkedTaskDto>>(Links
                .Where(l => l.NoteId == noteId)
                .Join(Tasks.Where(t => t.UserId == userId), l => l.TaskId, t => t.Task.Id, (_, t) => t.Task)
                .ToList());

        public Task<IReadOnlyList<NoteSummaryDto>> GetNotesForTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NoteSummaryDto>>(Links
                .Where(l => l.TaskId == taskId)
                .Join(Notes.Where(n => n.UserId == userId), l => l.NoteId, n => n.Id, (_, n) => new NoteSummaryDto(n.Id, n.Title, DateTime.UtcNow, null))
                .ToList());

        public Task<bool> ExistsAsync(Guid noteId, Guid taskId, CancellationToken cancellationToken) =>
            Task.FromResult(Links.Any(l => l.NoteId == noteId && l.TaskId == taskId));

        public Task<bool> AddAsync(NoteTaskLink link, CancellationToken cancellationToken)
        {
            if (FailNextAdd)
            {
                FailNextAdd = false;
                return Task.FromResult(false);
            }

            Links.Add(link);
            return Task.FromResult(true);
        }

        public Task RemoveAsync(Guid noteId, Guid taskId, CancellationToken cancellationToken)
        {
            Links.RemoveAll(l => l.NoteId == noteId && l.TaskId == taskId);
            return Task.CompletedTask;
        }
    }
}
