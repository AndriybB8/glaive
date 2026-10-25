namespace StudyPlanner.Domain.Schedules;

/// <summary>
/// Harmonogram użytkownika: nazwany okres nauki (np. „Sesja zimowa”) z zakresem dat.
/// Zakres to dni kalendarzowe (DateOnly) – nie mają strefy czasowej, więc nie przesuwają się przy konwersji.
/// Daty utworzenia i modyfikacji są w UTC.
/// </summary>
public class Schedule
{
    public const int NameMaxLength = 200;

    public Guid Id { get; private set; }

    /// <summary>Właściciel harmonogramu. W bazie: klucz obcy do tabeli users.</summary>
    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    /// <summary>Ostatni dzień harmonogramu (włącznie).</summary>
    public DateOnly EndDate { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Zwiększana przy każdej zmianie. Chroni przed nadpisaniem nowszej wersji
    /// (np. harmonogram edytowany w dwóch kartach). W EF Core oznaczyć jako concurrency token.
    /// </summary>
    public int Version { get; private set; }

    // Wymagany przez EF Core.
    private Schedule()
    {
    }

    public static Schedule Create(Guid userId, string name, DateOnly startDate, DateOnly endDate, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(name, startDate, endDate);

        return new Schedule
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Version = 1,
        };
    }

    public void Update(string name, DateOnly startDate, DateOnly endDate, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(name, startDate, endDate);

        Name = name;
        StartDate = startDate;
        EndDate = endDate;
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

    private static void EnsureValid(string name, DateOnly startDate, DateOnly endDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.Length > NameMaxLength)
        {
            throw new ArgumentException($"Nazwa może mieć maksymalnie {NameMaxLength} znaków.", nameof(name));
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("Data zakończenia nie może być wcześniejsza niż data rozpoczęcia.", nameof(endDate));
        }
    }
}
