using StudyPlanner.Application.Common;
using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

public sealed class TodoItemService(
    ITodoItemRepository items,
    ITodoListRepository lists,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly TodoItemError ItemNotFound = new(TodoItemErrorType.NotFound, "Nie znaleziono punktu listy.");
    private static readonly TodoItemError ListNotFound = new(TodoItemErrorType.NotFound, "Nie znaleziono listy.");

    /// <summary>Punkty listy w kolejności na liście (przy równej pozycji – starsze pierwsze).</summary>
    public async Task<TodoItemResult<IReadOnlyList<TodoItemDto>>> GetByListAsync(Guid listId, CancellationToken cancellationToken)
    {
        if (!await lists.ExistsAsync(listId, currentUser.UserId, cancellationToken))
        {
            return TodoItemResult<IReadOnlyList<TodoItemDto>>.Failure(ListNotFound);
        }

        var found = await items.GetByListAsync(listId, currentUser.UserId, cancellationToken);
        IReadOnlyList<TodoItemDto> result = found
            .OrderBy(i => i.Position)
            .ThenBy(i => i.CreatedAt)
            .Select(TodoItemDto.From)
            .ToList();

        return TodoItemResult<IReadOnlyList<TodoItemDto>>.Success(result);
    }

    public async Task<TodoItemResult<TodoItemDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await items.GetByIdAsync(id, currentUser.UserId, cancellationToken);

        return item is null
            ? TodoItemResult<TodoItemDto>.Failure(ItemNotFound)
            : TodoItemResult<TodoItemDto>.Success(TodoItemDto.From(item));
    }

    /// <summary>Dodaje niezrobiony punkt na końcu listy.</summary>
    public async Task<TodoItemResult<TodoItemDto>> CreateAsync(Guid listId, CreateTodoItemRequest request, CancellationToken cancellationToken)
    {
        if (!await lists.ExistsAsync(listId, currentUser.UserId, cancellationToken))
        {
            return TodoItemResult<TodoItemDto>.Failure(ListNotFound);
        }

        var title = request.Title?.Trim() ?? string.Empty;

        if (Validate(title) is { } error)
        {
            return TodoItemResult<TodoItemDto>.Failure(error);
        }

        // Dwa punkty dodane naraz mogą dostać tę samą pozycję – kolejność rozstrzyga wtedy data utworzenia.
        var existing = await items.GetByListAsync(listId, currentUser.UserId, cancellationToken);
        var position = existing.Count == 0 ? 0 : existing.Max(i => i.Position) + 1;

        var item = TodoItem.Create(currentUser.UserId, listId, title, position, UtcNow());
        await items.AddAsync(item, cancellationToken);

        return TodoItemResult<TodoItemDto>.Success(TodoItemDto.From(item));
    }

    public async Task<TodoItemResult<TodoItemDto>> UpdateAsync(Guid id, UpdateTodoItemRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim() ?? string.Empty;

        if (Validate(title) is { } error)
        {
            return TodoItemResult<TodoItemDto>.Failure(error);
        }

        var item = await items.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (item is null)
        {
            return TodoItemResult<TodoItemDto>.Failure(ItemNotFound);
        }

        if (item.Version != request.Version)
        {
            return TodoItemResult<TodoItemDto>.Failure(Conflict(item));
        }

        var isDone = request.IsDone ?? item.IsDone;

        // Zapis bez zmian nie podbija wersji ani daty.
        if (item.Title == title && item.IsDone == isDone)
        {
            return TodoItemResult<TodoItemDto>.Success(TodoItemDto.From(item));
        }

        item.Update(title, isDone, UtcNow());
        return await SaveAsync(item, cancellationToken);
    }

    /// <summary>Zaznaczenie / odznaczenie punktu (checkbox na liście).</summary>
    public async Task<TodoItemResult<TodoItemDto>> SetDoneAsync(Guid id, SetTodoItemDoneRequest request, CancellationToken cancellationToken)
    {
        if (request.IsDone is not { } isDone)
        {
            return TodoItemResult<TodoItemDto>.Failure(Invalid("Podaj, czy punkt jest zrobiony."));
        }

        var item = await items.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (item is null)
        {
            return TodoItemResult<TodoItemDto>.Failure(ItemNotFound);
        }

        if (item.IsDone == isDone)
        {
            return TodoItemResult<TodoItemDto>.Success(TodoItemDto.From(item));
        }

        item.SetDone(isDone, UtcNow());
        return await SaveAsync(item, cancellationToken);
    }

    public async Task<TodoItemResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await items.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (item is null)
        {
            return TodoItemResult<bool>.Failure(ItemNotFound);
        }

        await items.DeleteAsync(item, cancellationToken);
        return TodoItemResult<bool>.Success(true);
    }

    private async Task<TodoItemResult<TodoItemDto>> SaveAsync(TodoItem item, CancellationToken cancellationToken)
    {
        try
        {
            await items.UpdateAsync(item, cancellationToken);
        }
        catch (TodoItemConcurrencyException)
        {
            var current = await items.GetByIdAsync(item.Id, currentUser.UserId, cancellationToken);
            return current is null
                ? TodoItemResult<TodoItemDto>.Failure(ItemNotFound)
                : TodoItemResult<TodoItemDto>.Failure(Conflict(current));
        }

        return TodoItemResult<TodoItemDto>.Success(TodoItemDto.From(item));
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static TodoItemError Conflict(TodoItem current) =>
        new(TodoItemErrorType.Conflict, "Punkt listy został w międzyczasie zmieniony w innym miejscu.", TodoItemDto.From(current));

    private static TodoItemError? Validate(string title)
    {
        if (title.Length == 0)
        {
            return Invalid("Podaj treść punktu.");
        }

        if (title.Length > TodoItem.TitleMaxLength)
        {
            return Invalid($"Treść punktu może mieć maksymalnie {TodoItem.TitleMaxLength} znaków.");
        }

        return null;
    }

    private static TodoItemError Invalid(string message) => new(TodoItemErrorType.Validation, message);
}
