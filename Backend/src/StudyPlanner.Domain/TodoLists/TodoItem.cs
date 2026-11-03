namespace StudyPlanner.Domain.TodoLists;

/// <summary>
/// Punkt listy <see cref="TodoList"/>: tekst i znacznik „zrobione”. Kolejność na liście wyznacza <see cref="Position"/>.
/// Wszystkie daty są w UTC.
/// </summary>
public class TodoItem
{
    public const int TitleMaxLength = 200;

    public Guid Id { get; private set; }

    /// <summary>Lista, do której należy punkt. W bazie: klucz obcy do todo_lists (usuwanie kaskadowe).</summary>
    public Guid ListId { get; private set; }

    /// <summary>Właściciel – ten sam co listy. Trzymany osobno, żeby sprawdzać dostęp bez łączenia tabel.</summary>
    public Guid UserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public bool IsDone { get; private set; }

    /// <summary>Kiedy punkt oznaczono jako zrobiony; null, gdy nie jest zrobiony.</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>Miejsce na liście, od 0. Nowy punkt trafia na koniec.</summary>
    public int Position { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Zwiększana przy każdej zmianie (concurrency token w EF Core).</summary>
    public int Version { get; private set; }

    // Wymagany przez EF Core.
    private TodoItem()
    {
    }

    public static TodoItem Create(Guid userId, Guid listId, string title, int position, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(title);
        EnsureValidPosition(position);

        return new TodoItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ListId = listId,
            Title = title,
            IsDone = false,
            CompletedAt = null,
            Position = position,
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
        Touch(utcNow);
    }

    /// <summary>Zaznacza albo odznacza punkt. Ponowne zaznaczenie zrobionego punktu nie zmienia daty wykonania.</summary>
    public void SetDone(bool isDone, DateTime utcNow)
    {
        EnsureUtc(utcNow);

        if (IsDone == isDone)
        {
            return;
        }

        IsDone = isDone;
        CompletedAt = isDone ? utcNow : null;
        Touch(utcNow);
    }

    public void MoveTo(int position, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValidPosition(position);

        Position = position;
        Touch(utcNow);
    }

    private void Touch(DateTime utcNow)
    {
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
            throw new ArgumentException($"Treść punktu może mieć maksymalnie {TitleMaxLength} znaków.", nameof(title));
        }
    }

    private static void EnsureValidPosition(int position)
    {
        if (position < 0)
        {
            throw new ArgumentException("Pozycja na liście nie może być ujemna.", nameof(position));
        }
    }
}
