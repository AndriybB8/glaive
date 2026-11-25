using StudyPlanner.Application.Common;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Application.Tags;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Domain.Tags;

namespace StudyPlanner.UnitTests.Tags;

public class TagServiceTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid FriendId = Guid.NewGuid();

    private readonly FakeTagRepository _repository = new();
    private readonly FakeScheduleRepository _schedules = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly TestUser _currentUser = new(OwnerId);
    private readonly TagService _service;

    private readonly Schedule _schedule;

    public TagServiceTests()
    {
        _service = new TagService(_repository, _schedules, _currentUser, _time);

        _schedule = Schedule.Create(OwnerId, "Sesja zimowa", new DateOnly(2027, 1, 25), new DateOnly(2027, 2, 14), DateTime.UtcNow);
        _schedules.Schedules.Add(_schedule);
    }

    private void ShareWith(Guid userId, ScheduleMemberRole role) =>
        _schedules.Members.Add(new ScheduleMemberStub(_schedule.Id, userId, role));

    private Task<TagResult<TagDto>> CreateAsync(string name = "Matematyka", string color = "#3B82F6") =>
        _service.CreateAsync(_schedule.Id, new CreateTagRequest(name, color), CancellationToken.None);

    [Fact]
    public async Task Create_WithValidData_CreatesTag()
    {
        var result = await CreateAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Matematyka", result.Value!.Name);
        Assert.Equal("#3B82F6", result.Value.Color);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal(_schedule.Id, _repository.Tags.Single().ScheduleId);
    }

    [Fact]
    public async Task Create_WithTakenName_ReturnsValidationError()
    {
        await CreateAsync();

        var result = await CreateAsync("matematyka", "#FF0000");

        Assert.False(result.IsSuccess);
        Assert.Equal(TagErrorType.Validation, result.Error!.Type);
        Assert.Single(_repository.Tags);
    }

    [Fact]
    public async Task Create_SameNameDifferentSchedule_Succeeds()
    {
        var other = Schedule.Create(OwnerId, "Semestr letni", new DateOnly(2027, 2, 15), new DateOnly(2027, 6, 30), DateTime.UtcNow);
        _schedules.Schedules.Add(other);
        await CreateAsync();

        var result = await _service.CreateAsync(other.Id, new CreateTagRequest("Matematyka", "#FF0000"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _repository.Tags.Count);
    }

    [Fact]
    public async Task Create_WithInvalidColor_ReturnsValidationError()
    {
        var result = await CreateAsync(color: "blue");

        Assert.False(result.IsSuccess);
        Assert.Equal(TagErrorType.Validation, result.Error!.Type);
        Assert.Empty(_repository.Tags);
    }

    [Fact]
    public async Task Create_WithEmptyName_ReturnsValidationError()
    {
        var result = await CreateAsync(name: "");

        Assert.False(result.IsSuccess);
        Assert.Equal(TagErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Create_ByViewer_IsForbidden()
    {
        ShareWith(FriendId, ScheduleMemberRole.Viewer);
        _currentUser.UserId = FriendId;

        var result = await CreateAsync();

        Assert.Equal(TagErrorType.Forbidden, result.Error!.Type);
        Assert.Empty(_repository.Tags);
    }

    [Fact]
    public async Task Create_ByEditor_Succeeds()
    {
        ShareWith(FriendId, ScheduleMemberRole.Editor);
        _currentUser.UserId = FriendId;

        var result = await CreateAsync();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_InUnknownSchedule_IsNotFound()
    {
        var result = await _service.CreateAsync(Guid.NewGuid(), new CreateTagRequest("Matematyka", "#3B82F6"), CancellationToken.None);

        Assert.Equal(TagErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Update_WithCurrentVersion_SavesAndIncrementsVersion()
    {
        var created = (await CreateAsync()).Value!;
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.UpdateAsync(_schedule.Id, created.Id, new UpdateTagRequest("Fizyka", "#FF0000", created.Version), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Fizyka", result.Value!.Name);
        Assert.Equal("#FF0000", result.Value.Color);
        Assert.Equal(2, result.Value.Version);
        Assert.True(result.Value.UpdatedAt > created.UpdatedAt);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ReturnsConflictWithCurrentTag()
    {
        var created = (await CreateAsync()).Value!;
        await _service.UpdateAsync(_schedule.Id, created.Id, new UpdateTagRequest("Fizyka", "#FF0000", 1), CancellationToken.None);

        var result = await _service.UpdateAsync(_schedule.Id, created.Id, new UpdateTagRequest("Chemia", "#00FF00", 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(TagErrorType.Conflict, result.Error!.Type);
        Assert.Equal("Fizyka", result.Error.Current!.Name);
    }

    [Fact]
    public async Task Update_WithoutChanges_DoesNotIncrementVersion()
    {
        var created = (await CreateAsync()).Value!;

        var result = await _service.UpdateAsync(_schedule.Id, created.Id, new UpdateTagRequest("Matematyka", "#3B82F6", 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Version);
    }

    [Fact]
    public async Task Update_ByViewer_IsForbidden()
    {
        var created = (await CreateAsync()).Value!;
        ShareWith(FriendId, ScheduleMemberRole.Viewer);
        _currentUser.UserId = FriendId;

        var result = await _service.UpdateAsync(_schedule.Id, created.Id, new UpdateTagRequest("Fizyka", "#FF0000", created.Version), CancellationToken.None);

        Assert.Equal(TagErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task Delete_RemovesTag()
    {
        var created = (await CreateAsync()).Value!;

        var result = await _service.DeleteAsync(_schedule.Id, created.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_repository.Tags);
    }

    [Fact]
    public async Task Delete_ByViewer_IsForbidden()
    {
        var created = (await CreateAsync()).Value!;
        ShareWith(FriendId, ScheduleMemberRole.Viewer);
        _currentUser.UserId = FriendId;

        var result = await _service.DeleteAsync(_schedule.Id, created.Id, CancellationToken.None);

        Assert.Equal(TagErrorType.Forbidden, result.Error!.Type);
        Assert.Single(_repository.Tags);
    }

    [Fact]
    public async Task TagInScheduleWithoutAccess_IsNotFound()
    {
        var foreignSchedule = Schedule.Create(Guid.NewGuid(), "Cudzy", new DateOnly(2027, 1, 25), new DateOnly(2027, 2, 14), DateTime.UtcNow);
        _schedules.Schedules.Add(foreignSchedule);
        var foreign = Tag.Create(foreignSchedule.Id, "Cudzy", "#3B82F6", DateTime.UtcNow);
        _repository.Tags.Add(foreign);

        var get = await _service.GetByIdAsync(foreignSchedule.Id, foreign.Id, CancellationToken.None);
        var delete = await _service.DeleteAsync(foreignSchedule.Id, foreign.Id, CancellationToken.None);

        Assert.Equal(TagErrorType.NotFound, get.Error!.Type);
        Assert.Equal(TagErrorType.NotFound, delete.Error!.Type);
        Assert.Single(_repository.Tags);
    }

    [Fact]
    public async Task GetBySchedule_ByViewer_Succeeds()
    {
        await CreateAsync();
        ShareWith(FriendId, ScheduleMemberRole.Viewer);
        _currentUser.UserId = FriendId;

        var result = await _service.GetByScheduleAsync(_schedule.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task GetBySchedule_ReturnsSortedByName()
    {
        await CreateAsync("Zoologia");
        await CreateAsync("Algebra");

        var result = await _service.GetByScheduleAsync(_schedule.Id, CancellationToken.None);

        Assert.Equal(["Algebra", "Zoologia"], result.Value!.Select(t => t.Name));
    }

    private sealed class TestUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; set; } = userId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private sealed record ScheduleMemberStub(Guid ScheduleId, Guid UserId, ScheduleMemberRole Role);

    private sealed class FakeTagRepository : ITagRepository
    {
        public List<Tag> Tags { get; } = [];

        public Task<IReadOnlyList<Tag>> GetByScheduleAsync(Guid scheduleId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Tag>>(Tags.Where(t => t.ScheduleId == scheduleId).ToList());

        public Task<Tag?> GetByIdAsync(Guid id, Guid scheduleId, CancellationToken cancellationToken) =>
            Task.FromResult(Tags.FirstOrDefault(t => t.Id == id && t.ScheduleId == scheduleId));

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

    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public List<Schedule> Schedules { get; } = [];

        public List<ScheduleMemberStub> Members { get; } = [];

        public Task<IReadOnlyList<ScheduleWithAccess>> GetAccessibleByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(Schedule schedule, CancellationToken cancellationToken)
        {
            Schedules.Add(schedule);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Schedule schedule, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(Schedule schedule, CancellationToken cancellationToken)
        {
            Schedules.Remove(schedule);
            return Task.CompletedTask;
        }

        public Task<ScheduleWithAccess?> GetWithAccessAsync(Guid id, Guid userId, CancellationToken cancellationToken)
        {
            var schedule = Schedules.FirstOrDefault(s => s.Id == id);
            if (schedule is null)
            {
                return Task.FromResult<ScheduleWithAccess?>(null);
            }

            var access = schedule.UserId == userId
                ? AccessRole.Owner
                : Members.FirstOrDefault(m => m.ScheduleId == id && m.UserId == userId)?.Role.ToAccess();

            return Task.FromResult(access is { } value ? new ScheduleWithAccess(schedule, value) : null);
        }

        public Task<IReadOnlyList<Guid>> GetUserIdsWithAccessAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>(Schedules.FirstOrDefault(s => s.Id == id) is { } schedule
                ? [schedule.UserId, .. Members.Where(m => m.ScheduleId == id).Select(m => m.UserId)]
                : []);
    }
}
