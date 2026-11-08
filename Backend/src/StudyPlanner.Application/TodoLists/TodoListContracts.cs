using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

/// <param name="Access">Dostęp bieżącego użytkownika – frontend ukrywa na jego podstawie niedostępne akcje.</param>
public sealed record TodoListDto(Guid Id, string Title, DateTime CreatedAt, DateTime UpdatedAt, int Version, TodoListAccess Access)
{
    public static TodoListDto From(TodoList list, TodoListAccess access) =>
        new(list.Id, list.Title, list.CreatedAt, list.UpdatedAt, list.Version, access);
}

/// <summary>Lista na liście list – z licznikami do paska postępu („3 z 7”).</summary>
/// <param name="Access">Dostęp bieżącego użytkownika – frontend ukrywa na jego podstawie niedostępne akcje.</param>
public sealed record TodoListSummaryDto(
    Guid Id,
    string Title,
    int ItemCount,
    int DoneCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version,
    TodoListAccess Access)
{
    public static TodoListSummaryDto From(TodoListSummary summary) =>
        new(
            summary.List.Id,
            summary.List.Title,
            summary.ItemCount,
            summary.DoneCount,
            summary.List.CreatedAt,
            summary.List.UpdatedAt,
            summary.List.Version,
            summary.Access);
}

/// <summary>Nazwa jest opcjonalna, żeby brak wartości dało się zgłosić czytelnym komunikatem walidacji.</summary>
public sealed record CreateTodoListRequest(string? Title);

/// <summary><paramref name="Version"/> to wersja, którą klient ostatnio otrzymał z serwera.</summary>
public sealed record UpdateTodoListRequest(string? Title, int Version);
