namespace StudyPlanner.Domain.Tasks;

public enum TaskPriority
{
    Low,
    Medium,
    High,
}

/// <summary>Nazwa z „Item”, żeby nie mylić z System.Threading.Tasks.TaskStatus.</summary>
public enum TaskItemStatus
{
    ToDo,
    InProgress,
    Done,
}

/// <summary>
/// Zadanie w harmonogramie. Nazwa „TaskItem”, bo „Task” to typ asynchroniczny .NET.
/// Termin to dzień kalendarzowy (DateOnly), daty utworzenia i modyfikacji są w UTC.
/// </summary>
public class TaskItem
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    public Guid Id { get; private set; }

    /// <summary>Harmonogram, do którego należy zadanie. W bazie: klucz obcy do schedules (usuwanie kaskadowe).</summary>
    public Guid ScheduleId { get; private set; }

    /// <summary>Właściciel – ten sam co harmonogramu. Trzymany osobno, żeby sprawdzać dostęp bez łączenia tabel.</summary>
    public Guid UserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Opcjonalny opis (zwykły tekst). Brak opisu = null, nigdy pusty tekst.</summary>
    public string? Description { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public TaskPriority Priority { get; private set; }

    public TaskItemStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Zwiększana przy każdej zmianie (concurrency token w EF Core).</summary>
    public int Version { get; private set; }

    // Wymagany przez EF Core.
    private TaskItem()
    {
    }

    public static TaskItem Create(
        Guid userId,
        Guid scheduleId,
        string title,
        string? description,
        DateOnly? dueDate,
        TaskPriority priority,
        TaskItemStatus status,
        DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(title, description, priority, status);

        return new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ScheduleId = scheduleId,
            Title = title,
            Description = description,
            DueDate = dueDate,
            Priority = priority,
            Status = status,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Version = 1,
        };
    }

    public void Update(string title, string? description, DateOnly? dueDate, TaskPriority priority, TaskItemStatus status, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(title, description, priority, status);

        Title = title;
        Description = description;
        DueDate = dueDate;
        Priority = priority;
        Status = status;
        Touch(utcNow);
    }

    public void ChangeStatus(TaskItemStatus status, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(Title, Description, Priority, status);

        Status = status;
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

    private static void EnsureValid(string title, string? description, TaskPriority priority, TaskItemStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (title.Length > TitleMaxLength)
        {
            throw new ArgumentException($"Tytuł może mieć maksymalnie {TitleMaxLength} znaków.", nameof(title));
        }

        if (description is not null && (description.Length == 0 || description.Length > DescriptionMaxLength))
        {
            throw new ArgumentException($"Opis może mieć od 1 do {DescriptionMaxLength} znaków (brak opisu = null).", nameof(description));
        }

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
