namespace StudyPlanner.Domain.TodoLists;

/// <summary>Uprawnienia osoby, której udostępniono listę. Właściciel listy nie ma tu wpisu – wskazuje go <see cref="TodoList.UserId"/>.</summary>
public enum TodoListMemberRole
{
    Viewer,
    Editor,
}

/// <summary>
/// Osoba, której udostępniono listę <see cref="TodoList"/>. Wpis powstaje w chwili zaproszenia,
/// a dostęp do listy daje dopiero przyjęcie zaproszenia. Wszystkie daty są w UTC.
/// </summary>
public class TodoListMember
{
    public Guid ListId { get; private set; }

    public Guid UserId { get; private set; }

    public TodoListMemberRole Role { get; private set; }

    /// <summary>Kiedy wysłano zaproszenie.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Kiedy przyjęto zaproszenie; null, gdy jeszcze na nie nie odpowiedziano.</summary>
    public DateTime? AcceptedAt { get; private set; }

    // Wymagany przez EF Core.
    private TodoListMember()
    {
    }

    public static TodoListMember Invite(Guid listId, Guid userId, TodoListMemberRole role, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(role);

        return new TodoListMember
        {
            ListId = listId,
            UserId = userId,
            Role = role,
            CreatedAt = utcNow,
            AcceptedAt = null,
        };
    }

    /// <summary>Przyjmuje zaproszenie. Ponowne przyjęcie nie zmienia daty.</summary>
    public void Accept(DateTime utcNow)
    {
        EnsureUtc(utcNow);

        AcceptedAt ??= utcNow;
    }

    public void ChangeRole(TodoListMemberRole role)
    {
        EnsureValid(role);

        Role = role;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Data musi być w UTC.", nameof(value));
        }
    }

    private static void EnsureValid(TodoListMemberRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentException("Nieprawidłowe uprawnienia do listy.", nameof(role));
        }
    }
}
