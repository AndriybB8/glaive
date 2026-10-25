using StudyPlanner.Application.Common;
using StudyPlanner.Application.Notes;
using StudyPlanner.Domain.Notes;

namespace StudyPlanner.UnitTests.Notes;

public class NoteServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly FakeNoteRepository _repository = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly NoteService _service;

    public NoteServiceTests()
    {
        _service = new NoteService(_repository, new TestUser(UserId), _time);
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

        public Task<IReadOnlyList<Note>> GetByAuthorAsync(Guid authorId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Note>>(Notes.Where(n => n.AuthorId == authorId).ToList());

        public Task<Note?> GetByIdAsync(Guid id, Guid authorId, CancellationToken cancellationToken) =>
            Task.FromResult(Notes.FirstOrDefault(n => n.Id == id && n.AuthorId == authorId));

        public Task AddAsync(Note note, CancellationToken cancellationToken)
        {
            Notes.Add(note);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Note note, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(Note note, CancellationToken cancellationToken)
        {
            Notes.Remove(note);
            return Task.CompletedTask;
        }
    }
}
