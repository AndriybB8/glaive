using StudyPlanner.Application.Common;
using StudyPlanner.Application.NoteGroups;
using StudyPlanner.Domain.NoteGroups;

namespace StudyPlanner.UnitTests.NoteGroups;

public class NoteGroupServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly FakeNoteGroupRepository _repository = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
    private readonly NoteGroupService _service;

    public NoteGroupServiceTests()
    {
        _service = new NoteGroupService(_repository, new TestUser(UserId), _time);
    }

    [Fact]
    public async Task Create_TrimsNameAndSetsOwnerUtcDatesAndFirstVersion()
    {
        var result = await _service.CreateAsync(new CreateNoteGroupRequest("  Matematyka  "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var group = result.Value!;
        Assert.Equal("Matematyka", group.Name);
        Assert.Equal(1, group.Version);
        Assert.Equal(DateTimeKind.Utc, group.CreatedAt.Kind);
        Assert.Equal(group.CreatedAt, group.UpdatedAt);
        Assert.Equal(UserId, _repository.Groups.Single().UserId);
    }

    [Fact]
    public async Task Create_RejectsEmptyName()
    {
        var result = await _service.CreateAsync(new CreateNoteGroupRequest("   "), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(NoteGroupErrorType.Validation, result.Error!.Type);
        Assert.Empty(_repository.Groups);
    }

    [Fact]
    public async Task Create_RejectsTooLongName()
    {
        var name = new string('a', NoteGroup.NameMaxLength + 1);

        var result = await _service.CreateAsync(new CreateNoteGroupRequest(name), CancellationToken.None);

        Assert.Equal(NoteGroupErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Create_RejectsDuplicateNameIgnoringCase()
    {
        await _service.CreateAsync(new CreateNoteGroupRequest("Matematyka"), CancellationToken.None);

        var result = await _service.CreateAsync(new CreateNoteGroupRequest("MATEMATYKA"), CancellationToken.None);

        Assert.Equal(NoteGroupErrorType.Validation, result.Error!.Type);
        Assert.Single(_repository.Groups);
    }

    [Fact]
    public async Task Create_AllowsNameUsedByAnotherUser()
    {
        _repository.Groups.Add(NoteGroup.Create(Guid.NewGuid(), "Matematyka", DateTime.UtcNow));

        var result = await _service.CreateAsync(new CreateNoteGroupRequest("Matematyka"), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyOwnGroupsSortedByName()
    {
        await _service.CreateAsync(new CreateNoteGroupRequest("fizyka"), CancellationToken.None);
        await _service.CreateAsync(new CreateNoteGroupRequest("Analiza"), CancellationToken.None);
        _repository.Groups.Add(NoteGroup.Create(Guid.NewGuid(), "Cudza", DateTime.UtcNow));

        var result = await _service.GetAllAsync(CancellationToken.None);

        Assert.Equal(["Analiza", "fizyka"], result.Select(g => g.Name));
    }

    [Fact]
    public async Task Update_WithCurrentVersion_RenamesAndIncrementsVersion()
    {
        var created = (await _service.CreateAsync(new CreateNoteGroupRequest("Matma"), CancellationToken.None)).Value!;
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.UpdateAsync(created.Id, new UpdateNoteGroupRequest("Matematyka", created.Version), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Matematyka", result.Value!.Name);
        Assert.Equal(2, result.Value.Version);
        Assert.True(result.Value.UpdatedAt > created.UpdatedAt);
    }

    [Fact]
    public async Task Update_OnlyChangingLetterCase_IsAllowed()
    {
        var created = (await _service.CreateAsync(new CreateNoteGroupRequest("matma"), CancellationToken.None)).Value!;

        var result = await _service.UpdateAsync(created.Id, new UpdateNoteGroupRequest("Matma", created.Version), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Matma", result.Value!.Name);
    }

    [Fact]
    public async Task Update_ToNameOfOtherGroup_IsRejected()
    {
        await _service.CreateAsync(new CreateNoteGroupRequest("Fizyka"), CancellationToken.None);
        var created = (await _service.CreateAsync(new CreateNoteGroupRequest("Matma"), CancellationToken.None)).Value!;

        var result = await _service.UpdateAsync(created.Id, new UpdateNoteGroupRequest("fizyka", created.Version), CancellationToken.None);

        Assert.Equal(NoteGroupErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ReturnsConflictWithCurrentGroup()
    {
        var created = (await _service.CreateAsync(new CreateNoteGroupRequest("Matma"), CancellationToken.None)).Value!;
        await _service.UpdateAsync(created.Id, new UpdateNoteGroupRequest("Z innej karty", 1), CancellationToken.None);

        var result = await _service.UpdateAsync(created.Id, new UpdateNoteGroupRequest("Stara wersja", 1), CancellationToken.None);

        Assert.Equal(NoteGroupErrorType.Conflict, result.Error!.Type);
        Assert.Equal("Z innej karty", result.Error.Current!.Name);
    }

    [Fact]
    public async Task Update_WithoutChanges_DoesNotIncrementVersion()
    {
        var created = (await _service.CreateAsync(new CreateNoteGroupRequest("Matma"), CancellationToken.None)).Value!;

        var result = await _service.UpdateAsync(created.Id, new UpdateNoteGroupRequest(" Matma ", 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Version);
    }

    [Fact]
    public async Task Delete_RemovesGroup()
    {
        var created = (await _service.CreateAsync(new CreateNoteGroupRequest("Matma"), CancellationToken.None)).Value!;

        var result = await _service.DeleteAsync(created.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_repository.Groups);
    }

    [Fact]
    public async Task GroupOfAnotherUser_IsNotFound()
    {
        var foreign = NoteGroup.Create(Guid.NewGuid(), "Cudza", DateTime.UtcNow);
        _repository.Groups.Add(foreign);

        var get = await _service.GetByIdAsync(foreign.Id, CancellationToken.None);
        var update = await _service.UpdateAsync(foreign.Id, new UpdateNoteGroupRequest("Moja", 1), CancellationToken.None);
        var delete = await _service.DeleteAsync(foreign.Id, CancellationToken.None);

        Assert.Equal(NoteGroupErrorType.NotFound, get.Error!.Type);
        Assert.Equal(NoteGroupErrorType.NotFound, update.Error!.Type);
        Assert.Equal(NoteGroupErrorType.NotFound, delete.Error!.Type);
        Assert.Equal("Cudza", _repository.Groups.Single().Name);
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

    private sealed class FakeNoteGroupRepository : INoteGroupRepository
    {
        public List<NoteGroup> Groups { get; } = [];

        public Task<IReadOnlyList<NoteGroup>> GetByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NoteGroup>>(Groups.Where(g => g.UserId == userId).ToList());

        public Task<NoteGroup?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Groups.FirstOrDefault(g => g.Id == id && g.UserId == userId));

        public Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Groups.Any(g => g.Id == id && g.UserId == userId));

        public Task AddAsync(NoteGroup group, CancellationToken cancellationToken)
        {
            Groups.Add(group);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(NoteGroup group, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(NoteGroup group, CancellationToken cancellationToken)
        {
            Groups.Remove(group);
            return Task.CompletedTask;
        }
    }
}
