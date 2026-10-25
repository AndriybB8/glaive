using StudyPlanner.Domain.Notes;

namespace StudyPlanner.Application.Notes;

/// <param name="GroupId">Grupa notatki; null = „Bez grupy”.</param>
public sealed record NoteDto(
    Guid Id,
    string Title,
    string Content,
    Guid? GroupId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version)
{
    public static NoteDto From(Note note) =>
        new(note.Id, note.Title, note.Content, note.GroupId, note.CreatedAt, note.UpdatedAt, note.Version);
}

/// <summary>Wersja skrócona do listy notatek, bez treści.</summary>
/// <param name="GroupId">Grupa notatki; null = „Bez grupy”. Front filtruje po niej listę.</param>
public sealed record NoteSummaryDto(Guid Id, string Title, DateTime UpdatedAt, Guid? GroupId)
{
    public static NoteSummaryDto From(Note note) => new(note.Id, note.Title, note.UpdatedAt, note.GroupId);
}

/// <param name="GroupId">Opcjonalnie: grupa, do której od razu trafia nowa notatka.</param>
public sealed record CreateNoteRequest(string? Title, string? Content, Guid? GroupId = null);

/// <summary>
/// Używany zarówno przy ręcznym, jak i automatycznym zapisie.
/// <paramref name="Version"/> to wersja, którą klient ostatnio otrzymał z serwera.
/// </summary>
public sealed record UpdateNoteRequest(string? Title, string? Content, int Version);

/// <summary>Przeniesienie notatki do grupy. <paramref name="GroupId"/> = null przenosi do „Bez grupy”.</summary>
public sealed record MoveNoteRequest(Guid? GroupId);
