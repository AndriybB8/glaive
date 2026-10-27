namespace StudyPlanner.Domain.NoteTasks;

/// <summary>
/// Powiązanie notatki z zadaniem (wiele do wielu): notatka może dotyczyć kilku zadań,
/// a zadanie mieć kilka notatek. Usunięcie notatki albo zadania usuwa powiązanie (kaskada w bazie).
/// </summary>
public class NoteTaskLink
{
    public Guid NoteId { get; private set; }

    public Guid TaskId { get; private set; }

    /// <summary>Kiedy przypisano notatkę do zadania (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    // Wymagany przez EF Core.
    private NoteTaskLink()
    {
    }

    public static NoteTaskLink Create(Guid noteId, Guid taskId, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Data musi być w UTC.", nameof(utcNow));
        }

        return new NoteTaskLink
        {
            NoteId = noteId,
            TaskId = taskId,
            CreatedAt = utcNow,
        };
    }
}
