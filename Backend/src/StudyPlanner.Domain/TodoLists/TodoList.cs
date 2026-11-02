namespace StudyPlanner.Domain.TodoLists;

/// <summary>
/// Lista rzeczy do zrobienia (np. „Na egzamin z baz”) – prosta lista kontrolna, niezależna od harmonogramów.
/// Punkty listy to <see cref="TodoItem"/>. Wszystkie daty są w UTC.
/// </summary>
public class TodoList
{
    public const int TitleMaxLength = 100;

    public Guid Id { get; private set; }

    /// <summary>Właściciel listy. W bazie: klucz obcy do tabeli users.</summary>
    public Guid UserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Zwiększana przy każdej zmianie (concurrency token w EF Core).</summary>
    public int Version { get; private set; }

    // Wymagany przez EF Core.
    private TodoList()
    {
    }

    public static TodoList Create(Guid userId, string title, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(title);

        return new TodoList
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Version = 1,
        };
    }

    public void Rename(string title, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(title);

        Title = title;
        UpdatedAt = utcNow;
        Version++;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Data musi być w UTC.", nameof(value));
        }
    }

    private static void EnsureValid(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (title.Length > TitleMaxLength)
        {
            throw new ArgumentException($"Nazwa listy może mieć maksymalnie {TitleMaxLength} znaków.", nameof(title));
        }
    }
}
