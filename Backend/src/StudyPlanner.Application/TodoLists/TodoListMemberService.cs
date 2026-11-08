using StudyPlanner.Application.Common;
using StudyPlanner.Application.Users;
using StudyPlanner.Domain.TodoLists;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Application.TodoLists;

/// <summary>Udostępnianie list: zaproszenia, uprawnienia i opuszczanie listy.</summary>
public sealed class TodoListMemberService(
    ITodoListMemberRepository members,
    ITodoListRepository lists,
    IUserRepository users,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly TodoListError ListNotFound = new(TodoListErrorType.NotFound, "Nie znaleziono listy.");
    private static readonly TodoListError UserNotFound = new(TodoListErrorType.NotFound, "Nie znaleziono użytkownika o tym adresie.");
    private static readonly TodoListError MemberNotFound = new(TodoListErrorType.NotFound, "Ta osoba nie ma dostępu do listy.");
    private static readonly TodoListError InvitationNotFound = new(TodoListErrorType.NotFound, "Nie znaleziono zaproszenia.");
    private static readonly TodoListError Forbidden = new(TodoListErrorType.Forbidden, "Tylko właściciel może zarządzać dostępem do listy.");
    private static readonly TodoListError AlreadyMember = new(TodoListErrorType.Conflict, "Ta osoba ma już dostęp do listy albo zaproszenie.");

    /// <summary>Osoby z dostępem i oczekujące zaproszenia, od najstarszych – tylko właściciel.</summary>
    public async Task<TodoListResult<IReadOnlyList<TodoListMemberDto>>> GetByListAsync(Guid listId, CancellationToken cancellationToken)
    {
        if (await EnsureOwnerAsync(listId, cancellationToken) is { } denied)
        {
            return TodoListResult<IReadOnlyList<TodoListMemberDto>>.Failure(denied);
        }

        var found = await members.GetByListAsync(listId, cancellationToken);
        IReadOnlyList<TodoListMemberDto> result = found
            .OrderBy(m => m.Member.CreatedAt)
            .Select(TodoListMemberDto.From)
            .ToList();

        return TodoListResult<IReadOnlyList<TodoListMemberDto>>.Success(result);
    }

    /// <summary>Zaprasza osobę o podanym adresie. Dostęp do listy dostanie dopiero po przyjęciu zaproszenia.</summary>
    public async Task<TodoListResult<TodoListMemberDto>> InviteAsync(Guid listId, InviteTodoListMemberRequest request, CancellationToken cancellationToken)
    {
        // Najpierw uprawnienia: tylko właściciel może sprawdzać, czy dany adres ma konto.
        if (await EnsureOwnerAsync(listId, cancellationToken) is { } denied)
        {
            return TodoListResult<TodoListMemberDto>.Failure(denied);
        }

        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (email.Length == 0)
        {
            return TodoListResult<TodoListMemberDto>.Failure(Invalid("Podaj adres email."));
        }

        if (ValidateRole(request.Role) is { } invalidRole)
        {
            return TodoListResult<TodoListMemberDto>.Failure(invalidRole);
        }

        var user = await users.GetByEmailAsync(email, cancellationToken);
        if (user is null || user.Status != AccountStatus.Active)
        {
            return TodoListResult<TodoListMemberDto>.Failure(UserNotFound);
        }

        // Właściciel ma pełny dostęp bez wpisu w tabeli – właścicielem listy jest todo_lists.user_id.
        if (user.Id == currentUser.UserId)
        {
            return TodoListResult<TodoListMemberDto>.Failure(Invalid("Nie możesz zaprosić samego siebie."));
        }

        var member = TodoListMember.Invite(listId, user.Id, request.Role!.Value, UtcNow());

        return await members.AddAsync(member, cancellationToken) switch
        {
            AddTodoListMemberResult.Added => TodoListResult<TodoListMemberDto>.Success(
                TodoListMemberDto.From(new TodoListMemberWithEmail(member, user.Email))),
            AddTodoListMemberResult.AlreadyExists => TodoListResult<TodoListMemberDto>.Failure(AlreadyMember),
            _ => TodoListResult<TodoListMemberDto>.Failure(ListNotFound),
        };
    }

    /// <summary>Zmiana uprawnień – także przy zaproszeniu, na które jeszcze nie odpowiedziano.</summary>
    public async Task<TodoListResult<TodoListMemberDto>> ChangeRoleAsync(
        Guid listId,
        Guid userId,
        ChangeTodoListMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (await EnsureOwnerAsync(listId, cancellationToken) is { } denied)
        {
            return TodoListResult<TodoListMemberDto>.Failure(denied);
        }

        if (ValidateRole(request.Role) is { } invalidRole)
        {
            return TodoListResult<TodoListMemberDto>.Failure(invalidRole);
        }

        var found = await members.GetAsync(listId, userId, cancellationToken);
        if (found is null)
        {
            return TodoListResult<TodoListMemberDto>.Failure(MemberNotFound);
        }

        var role = request.Role!.Value;
        if (found.Member.Role != role)
        {
            found.Member.ChangeRole(role);

            if (!await members.UpdateAsync(found.Member, cancellationToken))
            {
                return TodoListResult<TodoListMemberDto>.Failure(MemberNotFound);
            }
        }

        return TodoListResult<TodoListMemberDto>.Success(TodoListMemberDto.From(found));
    }

    /// <summary>
    /// Właściciel odbiera dostęp albo cofa zaproszenie; osoba z dostępem może w ten sposób opuścić listę.
    /// Nikt inny nie może usuwać cudzego dostępu.
    /// </summary>
    public async Task<TodoListResult<bool>> RemoveAsync(Guid listId, Guid userId, CancellationToken cancellationToken)
    {
        var list = await lists.GetWithAccessAsync(listId, currentUser.UserId, cancellationToken);
        if (list is null)
        {
            return TodoListResult<bool>.Failure(ListNotFound);
        }

        if (!list.Access.CanManageList() && userId != currentUser.UserId)
        {
            return TodoListResult<bool>.Failure(Forbidden);
        }

        return await members.RemoveAsync(listId, userId, cancellationToken)
            ? TodoListResult<bool>.Success(true)
            : TodoListResult<bool>.Failure(MemberNotFound);
    }

    /// <summary>Zaproszenia bieżącego użytkownika, od najnowszych.</summary>
    public async Task<IReadOnlyList<TodoListInvitationDto>> GetInvitationsAsync(CancellationToken cancellationToken)
    {
        var found = await members.GetPendingByUserAsync(currentUser.UserId, cancellationToken);

        return found.OrderByDescending(i => i.InvitedAt).ToList();
    }

    /// <summary>Przyjęcie zaproszenia daje dostęp do listy. Ponowne przyjęcie niczego nie zmienia.</summary>
    public async Task<TodoListResult<bool>> AcceptInvitationAsync(Guid listId, CancellationToken cancellationToken)
    {
        var found = await members.GetAsync(listId, currentUser.UserId, cancellationToken);
        if (found is null)
        {
            return TodoListResult<bool>.Failure(InvitationNotFound);
        }

        if (found.Member.AcceptedAt is not null)
        {
            return TodoListResult<bool>.Success(true);
        }

        found.Member.Accept(UtcNow());

        return await members.UpdateAsync(found.Member, cancellationToken)
            ? TodoListResult<bool>.Success(true)
            : TodoListResult<bool>.Failure(InvitationNotFound);
    }

    /// <summary>Odrzucenie usuwa wpis, więc można zostać zaproszonym ponownie. Przyjętej listy nie odrzuca – to opuszczenie listy.</summary>
    public async Task<TodoListResult<bool>> DeclineInvitationAsync(Guid listId, CancellationToken cancellationToken)
    {
        var found = await members.GetAsync(listId, currentUser.UserId, cancellationToken);
        if (found is null || found.Member.AcceptedAt is not null)
        {
            return TodoListResult<bool>.Failure(InvitationNotFound);
        }

        return await members.RemoveAsync(listId, currentUser.UserId, cancellationToken)
            ? TodoListResult<bool>.Success(true)
            : TodoListResult<bool>.Failure(InvitationNotFound);
    }

    /// <summary>Null, gdy bieżący użytkownik jest właścicielem listy.</summary>
    private async Task<TodoListError?> EnsureOwnerAsync(Guid listId, CancellationToken cancellationToken)
    {
        var list = await lists.GetWithAccessAsync(listId, currentUser.UserId, cancellationToken);
        if (list is null)
        {
            return ListNotFound;
        }

        return list.Access.CanManageList() ? null : Forbidden;
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static TodoListError? ValidateRole(TodoListMemberRole? role) =>
        role is { } value && Enum.IsDefined(value) ? null : Invalid("Wybierz uprawnienia: podgląd albo edycja.");

    private static TodoListError Invalid(string message) => new(TodoListErrorType.Validation, message);
}
