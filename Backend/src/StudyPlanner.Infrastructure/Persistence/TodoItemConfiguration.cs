using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.TodoLists;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Infrastructure.Persistence;

/// <summary>Tabela todo_items – punkty list rzeczy do zrobienia. Usunięcie listy usuwa jej punkty (kaskada).</summary>
internal sealed class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.ToTable("todo_items", table =>
        {
            table.HasCheckConstraint("ck_todo_items_position", "position >= 0");

            // Data wykonania jest ustawiona dokładnie wtedy, gdy punkt jest zrobiony.
            table.HasCheckConstraint(
                "ck_todo_items_completed_at",
                "(is_done AND completed_at IS NOT NULL) OR (NOT is_done AND completed_at IS NULL)");
        });

        builder.HasKey(i => i.Id).HasName("pk_todo_items");
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(i => i.ListId).HasColumnName("list_id");
        builder.HasOne<TodoList>()
            .WithMany()
            .HasForeignKey(i => i.ListId)
            .HasConstraintName("fk_todo_items_todo_lists_list_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Punkty listy w kolejności. Indeks nie jest unikalny – przy zmianie kolejności dwa punkty mogą
        // chwilowo mieć tę samą pozycję. Służy też do wyszukiwania punktów listy (list_id jest pierwszy).
        builder.HasIndex(i => new { i.ListId, i.Position }).HasDatabaseName("ix_todo_items_list_id_position");

        builder.Property(i => i.UserId).HasColumnName("user_id");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .HasConstraintName("fk_todo_items_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(i => i.UserId).HasDatabaseName("ix_todo_items_user_id");

        builder.Property(i => i.Title).HasColumnName("title").HasMaxLength(TodoItem.TitleMaxLength).IsRequired();
        builder.Property(i => i.IsDone).HasColumnName("is_done").HasDefaultValue(false);
        builder.Property(i => i.CompletedAt).HasColumnName("completed_at");
        builder.Property(i => i.Position).HasColumnName("position");

        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");

        // Zapis sprawdza, czy wersja w bazie się nie zmieniła – stąd 409 przy edycji w dwóch kartach.
        builder.Property(i => i.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
