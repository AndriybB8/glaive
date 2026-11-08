using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

/// <summary>Osoba z dostępem do listy albo z oczekującym zaproszeniem (<paramref name="AcceptedAt"/> = null).</summary>
public sealed record TodoListMemberDto(Guid UserId, string Email, TodoListMemberRole Role, DateTime CreatedAt, DateTime? AcceptedAt)
{
    public static TodoListMemberDto From(TodoListMemberWithEmail found) =>
        new(found.Member.UserId, found.Email, found.Member.Role, found.Member.CreatedAt, found.Member.AcceptedAt);
}

/// <summary>Zaproszenie czekające na odpowiedź zaproszonej osoby.</summary>
public sealed record TodoListInvitationDto(Guid ListId, string ListTitle, string OwnerEmail, TodoListMemberRole Role, DateTime InvitedAt);

/// <summary>Pola są opcjonalne, żeby brak wartości dało się zgłosić czytelnym komunikatem walidacji.</summary>
public sealed record InviteTodoListMemberRequest(string? Email, TodoListMemberRole? Role);

public sealed record ChangeTodoListMemberRoleRequest(TodoListMemberRole? Role);
