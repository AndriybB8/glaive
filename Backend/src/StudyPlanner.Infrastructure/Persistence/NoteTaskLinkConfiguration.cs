using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.Notes;
using StudyPlanner.Domain.NoteTasks;
using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.Infrastructure.Persistence;

/// <summary>
/// Tabela note_tasks – łączy notatki z zadaniami (wiele do wielu).
/// Klucz złożony (note_id, task_id) nie pozwala przypisać tej samej pary dwa razy.
/// </summary>
internal sealed class NoteTaskLinkConfiguration : IEntityTypeConfiguration<NoteTaskLink>
{
    public void Configure(EntityTypeBuilder<NoteTaskLink> builder)
    {
        builder.ToTable("note_tasks");

        builder.HasKey(l => new { l.NoteId, l.TaskId }).HasName("pk_note_tasks");

        // Usunięcie notatki lub zadania usuwa ich powiązania.
        builder.Property(l => l.NoteId).HasColumnName("note_id");
        builder.HasOne<Note>()
            .WithMany()
            .HasForeignKey(l => l.NoteId)
            .HasConstraintName("fk_note_tasks_notes_note_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(l => l.TaskId).HasColumnName("task_id");
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(l => l.TaskId)
            .HasConstraintName("fk_note_tasks_tasks_task_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Wyszukiwanie notatek zadania. Wyszukiwanie zadań notatki obsługuje klucz główny (note_id jest pierwszy).
        builder.HasIndex(l => l.TaskId).HasDatabaseName("ix_note_tasks_task_id");

        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
    }
}
