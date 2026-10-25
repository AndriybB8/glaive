using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.Infrastructure.Persistence;

/// <summary>Tabela tasks – zadania harmonogramu. Usunięcie harmonogramu usuwa jego zadania (kaskada).</summary>
internal sealed class TaskConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks", table =>
        {
            table.HasCheckConstraint("ck_tasks_priority", "priority IN ('Low', 'Medium', 'High')");
            table.HasCheckConstraint("ck_tasks_status", "status IN ('ToDo', 'InProgress', 'Done')");
        });

        builder.HasKey(t => t.Id).HasName("pk_tasks");
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.ScheduleId).HasColumnName("schedule_id");
        builder.HasOne<Schedule>()
            .WithMany()
            .HasForeignKey(t => t.ScheduleId)
            .HasConstraintName("fk_tasks_schedules_schedule_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => t.ScheduleId).HasDatabaseName("ix_tasks_schedule_id");

        // TODO: klucz obcy do users(id), gdy powstanie tabela użytkowników (logowanie).
        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.HasIndex(t => t.UserId).HasDatabaseName("ix_tasks_user_id");

        builder.Property(t => t.Title).HasColumnName("title").HasMaxLength(TaskItem.TitleMaxLength).IsRequired();
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(TaskItem.DescriptionMaxLength);
        builder.Property(t => t.DueDate).HasColumnName("due_date");

        // Enumy jako tekst – czytelne w bazie i odporne na zmianę kolejności wartości w kodzie.
        builder.Property(t => t.Priority).HasColumnName("priority").HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        // Zapis sprawdza, czy wersja w bazie się nie zmieniła – stąd 409 przy edycji w dwóch kartach.
        builder.Property(t => t.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
