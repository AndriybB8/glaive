using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.TodoLists;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Infrastructure.Persistence;

/// <summary>Tabela todo_lists – listy rzeczy do zrobienia. Punkty list są w TodoItemConfiguration.</summary>
internal sealed class TodoListConfiguration : IEntityTypeConfiguration<TodoList>
{
    public void Configure(EntityTypeBuilder<TodoList> builder)
    {
        builder.ToTable("todo_lists");

        builder.HasKey(l => l.Id).HasName("pk_todo_lists");
        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(l => l.UserId).HasColumnName("user_id");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .HasConstraintName("fk_todo_lists_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(l => l.UserId).HasDatabaseName("ix_todo_lists_user_id");

        builder.Property(l => l.Title).HasColumnName("title").HasMaxLength(TodoList.TitleMaxLength).IsRequired();

        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");

        // Zapis sprawdza, czy wersja w bazie się nie zmieniła – stąd 409 przy zmianie nazwy w dwóch kartach.
        builder.Property(l => l.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
