namespace StudyPlanner.Domain.Notes;

/// <summary>
/// Notatka użytkownika. Treść przechowujemy jako HTML wygenerowany przez edytor (TipTap).
/// Wszystkie daty są w UTC.
/// </summary>
public class Note
{
    public const int TitleMaxLength = 200;
    public const int ContentMaxLength = 1_000_000;

    public Guid Id { get; private set; }

    public Guid AuthorId { get; private set; }

    /// <summary>
    /// Grupa (folder), do której należy notatka. Null = „Bez grupy”.
    /// W bazie: klucz obcy do note_groups – usunięcie grupy ustawia tu null.
    /// </summary>
    public Guid? GroupId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Zwiększana przy każdej zmianie. Chroni przed nadpisaniem nowszej wersji
    /// (np. notatka otwarta w dwóch kartach). W EF Core oznaczyć jako concurrency token.
    /// </summary>
    public int Version { get; private set; }

    // Wymagany przez EF Core.
    private Note()
    {
    }

    public static Note Create(Guid authorId, string title, string content, DateTime utcNow, Guid? groupId = null)
    {
        EnsureUtc(utcNow);
        EnsureValid(title, content);

        return new Note
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            GroupId = groupId,
            Title = title,
            Content = content,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Version = 1,
        };
    }

    public void Update(string title, string content, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(title, content);

        Title = title;
        Content = content;
        UpdatedAt = utcNow;
        Version++;
    }

    /// <summary>
    /// Przenosi notatkę do grupy (null = „Bez grupy”). Celowo nie zmienia wersji ani daty modyfikacji:
    /// przynależność do grupy to nie treść, a podbicie wersji wywołałoby konflikt autozapisu w otwartym edytorze.
    /// </summary>
    public void MoveToGroup(Guid? groupId)
    {
        GroupId = groupId;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Data musi być w UTC.", nameof(value));
        }
    }

    private static void EnsureValid(string title, string content)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(content);

        if (title.Length > TitleMaxLength)
        {
            throw new ArgumentException($"Tytuł może mieć maksymalnie {TitleMaxLength} znaków.", nameof(title));
        }

        if (content.Length > ContentMaxLength)
        {
            throw new ArgumentException($"Treść może mieć maksymalnie {ContentMaxLength} znaków.", nameof(content));
        }
    }
}
