using StudyPlanner.Domain.NoteGroups;

namespace StudyPlanner.Application.NoteGroups;

public sealed record NoteGroupDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version)
{
    public static NoteGroupDto From(NoteGroup group) =>
        new(group.Id, group.Name, group.CreatedAt, group.UpdatedAt, group.Version);
}

/// <summary>Nazwa jest opcjonalna, żeby brak wartości dało się zgłosić czytelnym komunikatem walidacji.</summary>
public sealed record CreateNoteGroupRequest(string? Name);

/// <summary><paramref name="Version"/> to wersja, którą klient ostatnio otrzymał z serwera.</summary>
public sealed record UpdateNoteGroupRequest(string? Name, int Version);
