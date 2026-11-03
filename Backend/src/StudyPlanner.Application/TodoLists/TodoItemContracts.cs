using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

public sealed record TodoItemDto(
    Guid Id,
    Guid ListId,
    string Title,
    bool IsDone,
    DateTime? CompletedAt,
    int Position,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version)
{
    public static TodoItemDto From(TodoItem item) =>
        new(item.Id, item.ListId, item.Title, item.IsDone, item.CompletedAt, item.Position, item.CreatedAt, item.UpdatedAt, item.Version);
}

/// <summary>Nowy punkt jest niezrobiony i trafia na koniec listy.</summary>
public sealed record CreateTodoItemRequest(string? Title);

/// <summary>
/// Brak <paramref name="IsDone"/> zostawia obecny stan.
/// <paramref name="Version"/> to wersja, którą klient ostatnio otrzymał z serwera.
/// </summary>
public sealed record UpdateTodoItemRequest(string? Title, bool? IsDone, int Version);

/// <summary>Zaznaczenie / odznaczenie z listy – bez kontroli wersji (wygrywa ostatnia zmiana).</summary>
public sealed record SetTodoItemDoneRequest(bool? IsDone);
