namespace StudyPlanner.Domain.NoteGroups;

/// <summary>
/// Grupa notatek (folder/kategoria, np. „Matematyka”). Notatka należy do jednej grupy albo do żadnej.
/// Grupy są płaskie – bez zagnieżdżania. Wszystkie daty są w UTC.
/// </summary>
public class NoteGroup
{
    public const int NameMaxLength = 100;

    public Guid Id { get; private set; }

    /// <summary>Właściciel grupy. W bazie: klucz obcy do tabeli users (gdy powstanie).</summary>
    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Zwiększana przy każdej zmianie (concurrency token w EF Core).</summary>
    public int Version { get; private set; }

    // Wymagany przez EF Core.
    private NoteGroup()
    {
    }

    public static NoteGroup Create(Guid userId, string name, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(name);

        return new NoteGroup
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Version = 1,
        };
    }

    public void Rename(string name, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(name);

        Name = name;
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

    private static void EnsureValid(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.Length > NameMaxLength)
        {
            throw new ArgumentException($"Nazwa grupy może mieć maksymalnie {NameMaxLength} znaków.", nameof(name));
        }
    }
}
