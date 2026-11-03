using StudyPlanner.Application.Common;
using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

public sealed class TodoListService(ITodoListRepository repository, ICurrentUser currentUser, TimeProvider timeProvider)
{
    private static readonly TodoListError NotFound = new(TodoListErrorType.NotFound, "Nie znaleziono listy.");

    /// <summary>Listy użytkownika z licznikami punktów, posortowane alfabetycznie.</summary>
    public async Task<IReadOnlyList<TodoListSummaryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var summaries = await repository.GetSummariesByUserAsync(currentUser.UserId, cancellationToken);

        return summaries
            .OrderBy(s => s.List.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(s => s.List.CreatedAt)
            .Select(TodoListSummaryDto.From)
            .ToList();
    }

    public async Task<TodoListResult<TodoListDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var list = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);

        return list is null
            ? TodoListResult<TodoListDto>.Failure(NotFound)
            : TodoListResult<TodoListDto>.Success(TodoListDto.From(list));
    }

    public async Task<TodoListResult<TodoListDto>> CreateAsync(CreateTodoListRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim() ?? string.Empty;

        if (Validate(title) is { } error)
        {
            return TodoListResult<TodoListDto>.Failure(error);
        }

        var list = TodoList.Create(currentUser.UserId, title, UtcNow());
        await repository.AddAsync(list, cancellationToken);

        return TodoListResult<TodoListDto>.Success(TodoListDto.From(list));
    }

    /// <summary>Zmiana nazwy listy.</summary>
    public async Task<TodoListResult<TodoListDto>> UpdateAsync(Guid id, UpdateTodoListRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim() ?? string.Empty;

        if (Validate(title) is { } error)
        {
            return TodoListResult<TodoListDto>.Failure(error);
        }

        var list = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (list is null)
        {
            return TodoListResult<TodoListDto>.Failure(NotFound);
        }

        if (list.Version != request.Version)
        {
            return TodoListResult<TodoListDto>.Failure(Conflict(list));
        }

        // Zapis bez zmian nie podbija wersji ani daty.
        if (list.Title == title)
        {
            return TodoListResult<TodoListDto>.Success(TodoListDto.From(list));
        }

        list.Rename(title, UtcNow());

        try
        {
            await repository.UpdateAsync(list, cancellationToken);
        }
        catch (TodoListConcurrencyException)
        {
            var current = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
            return current is null
                ? TodoListResult<TodoListDto>.Failure(NotFound)
                : TodoListResult<TodoListDto>.Failure(Conflict(current));
        }

        return TodoListResult<TodoListDto>.Success(TodoListDto.From(list));
    }

    /// <summary>Usuwa listę razem z jej punktami.</summary>
    public async Task<TodoListResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var list = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (list is null)
        {
            return TodoListResult<bool>.Failure(NotFound);
        }

        await repository.DeleteAsync(list, cancellationToken);
        return TodoListResult<bool>.Success(true);
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static TodoListError Conflict(TodoList current) =>
        new(TodoListErrorType.Conflict, "Lista została w międzyczasie zmieniona w innym miejscu.", TodoListDto.From(current));

    private static TodoListError? Validate(string title)
    {
        if (title.Length == 0)
        {
            return Invalid("Podaj nazwę listy.");
        }

        if (title.Length > TodoList.TitleMaxLength)
        {
            return Invalid($"Nazwa listy może mieć maksymalnie {TodoList.TitleMaxLength} znaków.");
        }

        return null;
    }

    private static TodoListError Invalid(string message) => new(TodoListErrorType.Validation, message);
}
