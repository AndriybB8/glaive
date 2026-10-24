using System.Collections.Concurrent;
using StudyPlanner.Application.Notes;
using StudyPlanner.Domain.Notes;

namespace StudyPlanner.Infrastructure.Notes;

/// <summary>
/// TYMCZASOWE repozytorium w pamięci, żeby API i frontend działały przed podłączeniem bazy.
/// Dane znikają po restarcie backendu. Do zastąpienia implementacją opartą na EF Core
/// (rejestracja w Program.cs).
/// </summary>
public sealed class InMemoryNoteRepository : INoteRepository
{
    private readonly ConcurrentDictionary<Guid, Note> _notes = new();

    public Task<IReadOnlyList<Note>> GetByAuthorAsync(Guid authorId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Note> result = _notes.Values.Where(n => n.AuthorId == authorId).ToList();
        return Task.FromResult(result);
    }

    public Task<Note?> GetByIdAsync(Guid id, Guid authorId, CancellationToken cancellationToken)
    {
        var note = _notes.TryGetValue(id, out var found) && found.AuthorId == authorId ? found : null;
        return Task.FromResult(note);
    }

    public Task AddAsync(Note note, CancellationToken cancellationToken)
    {
        _notes[note.Id] = note;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Note note, CancellationToken cancellationToken)
    {
        _notes[note.Id] = note;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Note note, CancellationToken cancellationToken)
    {
        _notes.TryRemove(note.Id, out _);
        return Task.CompletedTask;
    }
}
