using StudyPlanner.Application.Common;
using StudyPlanner.Application.NoteGroups;
using StudyPlanner.Application.Notes;
using StudyPlanner.Domain.NoteGroups;
using StudyPlanner.Domain.Notes;

namespace StudyPlanner.UnitTests.Notes;

public class NoteServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly FakeNoteRepository _repository = new();
    private readonly FakeNoteGroupRepository _groups = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly NoteService _service;

    public NoteServiceTests()
    {
        _service = new NoteService(_repository, _groups, new TestUser(UserId), _time);
    }

    [Fact]
    public async Task Create_SetsAuthorUtcDatesAndFirstVersion()
    {
        var result = await _service.CreateAsync(new CreateNoteRequest("  Algebra  ", "<p>Macierze</p>"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var note = result.Value!;
        Assert.Equal("Algebra", note.Title);
        Assert.Equal(1, note.Version);
        Assert.Equal(DateTimeKind.Utc, note.CreatedAt.Kind);
        Assert.Equal(note.CreatedAt, note.UpdatedAt);
        Assert.Equal(UserId, _repository.Notes.Single().AuthorId);
    }

    [Fact]
    public async Task Create_RejectsTooLongTitle()
    {
        var title = new string('a', Note.TitleMaxLength + 1);

        var result = await _service.CreateAsync(new CreateNoteRequest(title, ""), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(NoteErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Update_WithCurrentVersion_SavesAndIncrementsVersion()
    {
        var created = (await _service.CreateAsync(new CreateNoteRequest("Tytuł", "<p>a</p>"), CancellationToken.None)).Value!;
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.UpdateAsync(created.Id, new UpdateNoteRequest("Tytuł", "<p>b</p>", created.Version), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("<p>b</p>", result.Value!.Content);
        Assert.Equal(2, result.Value.Version);
        Assert.True(result.Value.UpdatedAt > created.UpdatedAt);
        Assert.Equal(created.CreatedAt, result.Value.CreatedAt);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ReturnsConflictWithCurrentNote()
    {
        var created = (await _service.CreateAsync(new CreateNoteRequest("Tytuł", "<p>a</p>"), CancellationToken.None)).Value!;
        await _service.UpdateAsync(created.Id, new UpdateNoteRequest("Tytuł", "<p>z innej karty</p>", 1), CancellationToken.None);

        var result = await _service.UpdateAsync(created.Id, new UpdateNoteRequest("Tytuł", "<p>stara wersja</p>", 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(NoteErrorType.Conflict, result.Error!.Type);
        Assert.Equal("<p>z innej karty</p>", result.Error.Current!.Content);
    }

    [Fact]
    public async Task Update_WithoutChanges_DoesNotIncrementVersion()
    {
        var created = (await _service.CreateAsync(new CreateNoteRequest("Tytuł", "<p>a</p>"), CancellationToken.None)).Value!;

        var result = await _service.UpdateAsync(created.Id, new UpdateNoteRequest("Tytuł", "<p>a</p>", 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Version);
    }

    [Fact]
    public async Task NoteOfAnotherUser_IsNotFound()
    {
        var foreign = Note.Create(Guid.NewGuid(), "Cudza", "", DateTime.UtcNow);
        await _repository.AddAsync(foreign, CancellationToken.None);

        var get = await _service.GetByIdAsync(foreign.Id, CancellationToken.None);
        var delete = await _service.DeleteAsync(foreign.Id, CancellationToken.None);

        Assert.Equal(NoteErrorType.NotFound, get.Error!.Type);
        Assert.Equal(NoteErrorType.NotFound, delete.Error!.Type);
        Assert.Single(_repository.Notes);
    }

    [Fact]
    public async Task Create_InGroup_SetsGroupId()
    {
        var group = AddGroup(UserId, "Matematyka");

        var result = await _service.CreateAsync(new CreateNoteRequest("Macierze", "", group.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(group.Id, result.Value!.GroupId);
    }

    [Fact]
    public async Task Create_InGroupOfAnotherUser_IsRejected()
    {
        var foreignGroup = AddGroup(Guid.NewGuid(), "Cudza");

        var result = await _service.CreateAsync(new CreateNoteRequest("Tytuł", "", foreignGroup.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(NoteErrorType.Validation, result.Error!.Type);
        Assert.Empty(_repository.Notes);
    }

    [Fact]
    public async Task MoveToGroup_ChangesGroupButNotVersionOrDate()
    {
        var group = AddGroup(UserId, "Fizyka");
        var created = (await _service.CreateAsync(new CreateNoteRequest("Ruch", "<p>a</p>"), CancellationToken.None)).Value!;
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.MoveToGroupAsync(created.Id, new MoveNoteRequest(group.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var note = (await _service.GetByIdAsync(created.Id, CancellationToken.None)).Value!;
        Assert.Equal(group.Id, note.GroupId);
        Assert.Equal(created.Version, note.Version);
        Assert.Equal(created.UpdatedAt, note.UpdatedAt);
    }

    [Fact]
    public async Task MoveToGroup_DoesNotBreakAutosaveWithVersionFromBeforeMove()
    {
        var group = AddGroup(UserId, "Fizyka");
        var created = (await _service.CreateAsync(new CreateNoteRequest("Ruch", "<p>a</p>"), CancellationToken.None)).Value!;
        await _service.MoveToGroupAsync(created.Id, new MoveNoteRequest(group.Id), CancellationToken.None);

        // Edytor nadal ma wersję sprzed przeniesienia.
        var result = await _service.UpdateAsync(created.Id, new UpdateNoteRequest("Ruch", "<p>b</p>", created.Version), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(group.Id, result.Value!.GroupId);
    }

    [Fact]
    public async Task MoveToGroup_WithNull_MovesToUngrouped()
    {
        var group = AddGroup(UserId, "Fizyka");
        var created = (await _service.CreateAsync(new CreateNoteRequest("Ruch", "", group.Id), CancellationToken.None)).Value!;

        var result = await _service.MoveToGroupAsync(created.Id, new MoveNoteRequest(null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(_repository.Notes.Single().GroupId);
    }

    [Fact]
    public async Task MoveToGroup_ToMissingGroup_IsRejected()
    {
        var created = (await _service.CreateAsync(new CreateNoteRequest("Ruch", ""), CancellationToken.None)).Value!;

        var result = await _service.MoveToGroupAsync(created.Id, new MoveNoteRequest(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(NoteErrorType.Validation, result.Error!.Type);
        Assert.Null(_repository.Notes.Single().GroupId);
    }

    [Fact]
    public async Task MoveToGroup_NoteOfAnotherUser_IsNotFound()
    {
        var group = AddGroup(UserId, "Fizyka");
        var foreign = Note.Create(Guid.NewGuid(), "Cudza", "", DateTime.UtcNow);
        await _repository.AddAsync(foreign, CancellationToken.None);

        var result = await _service.MoveToGroupAsync(foreign.Id, new MoveNoteRequest(group.Id), CancellationToken.None);

        Assert.Equal(NoteErrorType.NotFound, result.Error!.Type);
        Assert.Null(foreign.GroupId);
    }

    [Fact]
    public async Task GetAll_ReturnsGroupIdOfEachNote()
    {
        var group = AddGroup(UserId, "Fizyka");
        await _service.CreateAsync(new CreateNoteRequest("W grupie", "", group.Id), CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(1));
        await _service.CreateAsync(new CreateNoteRequest("Bez grupy", ""), CancellationToken.None);

        var notes = await _service.GetAllAsync(CancellationToken.None);

        Assert.Equal(["Bez grupy", "W grupie"], notes.Select(n => n.Title));
        Assert.Null(notes[0].GroupId);
        Assert.Equal(group.Id, notes[1].GroupId);
    }

    private NoteGroup AddGroup(Guid userId, string name)
    {
        var group = NoteGroup.Create(userId, name, DateTime.UtcNow);
        _groups.Groups.Add(group);
        return group;
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

    private sealed class FakeNoteRepository : INoteRepository
    {
        public List<Note> Notes { get; } = [];

        public Task<IReadOnlyList<NoteSummaryDto>> GetSummariesByAuthorAsync(Guid authorId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NoteSummaryDto>>(Notes.Where(n => n.AuthorId == authorId).Select(NoteSummaryDto.From).ToList());

        public Task<Note?> GetByIdAsync(Guid id, Guid authorId, CancellationToken cancellationToken) =>
            Task.FromResult(Notes.FirstOrDefault(n => n.Id == id && n.AuthorId == authorId));

        public Task AddAsync(Note note, CancellationToken cancellationToken)
        {
            Notes.Add(note);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Note note, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> MoveToGroupAsync(Guid id, Guid authorId, Guid? groupId, CancellationToken cancellationToken)
        {
            var note = Notes.FirstOrDefault(n => n.Id == id && n.AuthorId == authorId);
            note?.MoveToGroup(groupId);
            return Task.FromResult(note is not null);
        }

        public Task DeleteAsync(Note note, CancellationToken cancellationToken)
        {
            Notes.Remove(note);
            return Task.CompletedTask;
        }
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
