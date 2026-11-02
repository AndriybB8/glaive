using StudyPlanner.Application.Common;
using StudyPlanner.Application.Tags;
using StudyPlanner.Domain.Tags;

namespace StudyPlanner.UnitTests.Tags;

public class TagServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly FakeTagRepository _repository = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly TagService _service;

    public TagServiceTests()
    {
        _service = new TagService(_repository, new TestUser(UserId), _time);
    }

    [Fact]
    public async Task Create_WithValidData_CreatesTag()
    {
        var result = await _service.CreateAsync(new CreateTagRequest("Matematyka", "#3B82F6"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Matematyka", result.Value!.Name);
        Assert.Equal("#3B82F6", result.Value.Color);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal(UserId, _repository.Tags.Single().UserId);
    }

    [Fact]
    public async Task Create_WithTakenName_ReturnsValidationError()
    {
        await _service.CreateAsync(new CreateTagRequest("Matematyka", "#3B82F6"), CancellationToken.None);

        var result = await _service.CreateAsync(new CreateTagRequest("matematyka", "#FF0000"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(TagErrorType.Validation, result.Error!.Type);
        Assert.Single(_repository.Tags);
    }

    [Fact]
    public async Task Create_WithInvalidColor_ReturnsValidationError()
    {
        var result = await _service.CreateAsync(new CreateTagRequest("Matematyka", "blue"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(TagErrorType.Validation, result.Error!.Type);
        Assert.Empty(_repository.Tags);
    }

    [Fact]
    public async Task Create_WithEmptyName_ReturnsValidationError()
    {
        var result = await _service.CreateAsync(new CreateTagRequest("", "#3B82F6"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(TagErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Update_WithCurrentVersion_SavesAndIncrementsVersion()
    {
        var created = (await _service.CreateAsync(new CreateTagRequest("Matematyka", "#3B82F6"), CancellationToken.None)).Value!;
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.UpdateAsync(created.Id, new UpdateTagRequest("Fizyka", "#FF0000", created.Version), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Fizyka", result.Value!.Name);
        Assert.Equal("#FF0000", result.Value.Color);
        Assert.Equal(2, result.Value.Version);
        Assert.True(result.Value.UpdatedAt > created.UpdatedAt);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ReturnsConflictWithCurrentTag()
    {
        var created = (await _service.CreateAsync(new CreateTagRequest("Matematyka", "#3B82F6"), CancellationToken.None)).Value!;
        await _service.UpdateAsync(created.Id, new UpdateTagRequest("Fizyka", "#FF0000", 1), CancellationToken.None);

        var result = await _service.UpdateAsync(created.Id, new UpdateTagRequest("Chemia", "#00FF00", 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(TagErrorType.Conflict, result.Error!.Type);
        Assert.Equal("Fizyka", result.Error.Current!.Name);
    }

    [Fact]
    public async Task Update_WithoutChanges_DoesNotIncrementVersion()
    {
        var created = (await _service.CreateAsync(new CreateTagRequest("Matematyka", "#3B82F6"), CancellationToken.None)).Value!;

        var result = await _service.UpdateAsync(created.Id, new UpdateTagRequest("Matematyka", "#3B82F6", 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Version);
    }

    [Fact]
    public async Task Delete_RemovesTag()
    {
        var created = (await _service.CreateAsync(new CreateTagRequest("Matematyka", "#3B82F6"), CancellationToken.None)).Value!;

        var result = await _service.DeleteAsync(created.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_repository.Tags);
    }

    [Fact]
    public async Task TagOfAnotherUser_IsNotFound()
    {
        var foreign = Tag.Create(Guid.NewGuid(), "Cudzy", "#3B82F6", DateTime.UtcNow);
        _repository.Tags.Add(foreign);

        var get = await _service.GetByIdAsync(foreign.Id, CancellationToken.None);
        var delete = await _service.DeleteAsync(foreign.Id, CancellationToken.None);

        Assert.Equal(TagErrorType.NotFound, get.Error!.Type);
        Assert.Equal(TagErrorType.NotFound, delete.Error!.Type);
        Assert.Single(_repository.Tags);
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
}
