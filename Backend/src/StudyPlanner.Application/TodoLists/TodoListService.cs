using StudyPlanner.Application.Common;
using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

public sealed class TodoListService(ITodoListRepository repository, ICurrentUser currentUser, TimeProvider timeProvider)
{
    private static readonly TodoListError NotFound = new(TodoListErrorType.NotFound, "Nie znaleziono listy.");
    private static readonly TodoListError Forbidden = new(TodoListErrorType.Forbidden, "Tylko właściciel może zmienić nazwę listy albo ją usunąć.");

    /// <summary>Listy użytkownika i udostępnione mu listy z licznikami punktów, posortowane alfabetycznie.</summary>
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
        var found = await repository.GetWithAccessAsync(id, currentUser.UserId, cancellationToken);

        return found is null
            ? TodoListResult<TodoListDto>.Failure(NotFound)
            : TodoListResult<TodoListDto>.Success(TodoListDto.From(found.List, found.Access));
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

        return TodoListResult<TodoListDto>.Success(ForOwner(list));
    }

    /// <summary>Zmiana nazwy listy – tylko właściciel.</summary>
    public async Task<TodoListResult<TodoListDto>> UpdateAsync(Guid id, UpdateTodoListRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim() ?? string.Empty;

        if (Validate(title) is { } error)
        {
            return TodoListResult<TodoListDto>.Failure(error);
        }

        var found = await repository.GetWithAccessAsync(id, currentUser.UserId, cancellationToken);
        if (found is null)
        {
            return TodoListResult<TodoListDto>.Failure(NotFound);
        }

        if (!found.Access.CanManageList())
        {
            return TodoListResult<TodoListDto>.Failure(Forbidden);
        }

        var list = found.List;

        if (list.Version != request.Version)
        {
            return TodoListResult<TodoListDto>.Failure(Conflict(list));
        }

        // Zapis bez zmian nie podbija wersji ani daty.
        if (list.Title == title)
        {
            return TodoListResult<TodoListDto>.Success(ForOwner(list));
        }

        list.Rename(title, UtcNow());

        try
        {
            await repository.UpdateAsync(list, cancellationToken);
        }
        catch (TodoListConcurrencyException)
        {
            var current = await repository.GetWithAccessAsync(id, currentUser.UserId, cancellationToken);
            return current is null
                ? TodoListResult<TodoListDto>.Failure(NotFound)
                : TodoListResult<TodoListDto>.Failure(Conflict(current.List));
        }

        return TodoListResult<TodoListDto>.Success(ForOwner(list));
    }

    /// <summary>Usuwa listę razem z jej punktami i osobami z dostępem – tylko właściciel.</summary>
    public async Task<TodoListResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await repository.GetWithAccessAsync(id, currentUser.UserId, cancellationToken);
        if (found is null)
        {
            return TodoListResult<bool>.Failure(NotFound);
        }

        if (!found.Access.CanManageList())
        {
            return TodoListResult<bool>.Failure(Forbidden);
        }

        await repository.DeleteAsync(found.List, cancellationToken);
        return TodoListResult<bool>.Success(true);
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static TodoListDto ForOwner(TodoList list) => TodoListDto.From(list, TodoListAccess.Owner);

    // Do konfliktu dochodzi tylko przy zmianie nazwy, a tę może wykonać wyłącznie właściciel.
    private static TodoListError Conflict(TodoList current) =>
        new(TodoListErrorType.Conflict, "Lista została w międzyczasie zmieniona w innym miejscu.", ForOwner(current));

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
