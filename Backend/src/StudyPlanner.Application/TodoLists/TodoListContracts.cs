using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

public sealed record TodoListDto(Guid Id, string Title, DateTime CreatedAt, DateTime UpdatedAt, int Version)
{
    public static TodoListDto From(TodoList list) =>
        new(list.Id, list.Title, list.CreatedAt, list.UpdatedAt, list.Version);
}

/// <summary>Lista na liście list – z licznikami do paska postępu („3 z 7”).</summary>
public sealed record TodoListSummaryDto(
    Guid Id,
    string Title,
    int ItemCount,
    int DoneCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version)
{
    public static TodoListSummaryDto From(TodoListSummary summary) =>
        new(
            summary.List.Id,
            summary.List.Title,
            summary.ItemCount,
            summary.DoneCount,
            summary.List.CreatedAt,
            summary.List.UpdatedAt,
            summary.List.Version);
}

/// <summary>Nazwa jest opcjonalna, żeby brak wartości dało się zgłosić czytelnym komunikatem walidacji.</summary>
public sealed record CreateTodoListRequest(string? Title);

/// <summary><paramref name="Version"/> to wersja, którą klient ostatnio otrzymał z serwera.</summary>
public sealed record UpdateTodoListRequest(string? Title, int Version);
