using StudyPlanner.Domain.Notes;

namespace StudyPlanner.Application.Notes;

public sealed record NoteDto(
    Guid Id,
    string Title,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version)
{
    public static NoteDto From(Note note) =>
        new(note.Id, note.Title, note.Content, note.CreatedAt, note.UpdatedAt, note.Version);
}

/// <summary>Wersja skrócona do listy notatek, bez treści.</summary>
public sealed record NoteSummaryDto(Guid Id, string Title, DateTime UpdatedAt)
{
    public static NoteSummaryDto From(Note note) => new(note.Id, note.Title, note.UpdatedAt);
}

public sealed record CreateNoteRequest(string? Title, string? Content);

/// <summary>
/// Używany zarówno przy ręcznym, jak i automatycznym zapisie.
/// <paramref name="Version"/> to wersja, którą klient ostatnio otrzymał z serwera.
/// </summary>
public sealed record UpdateNoteRequest(string? Title, string? Content, int Version);
