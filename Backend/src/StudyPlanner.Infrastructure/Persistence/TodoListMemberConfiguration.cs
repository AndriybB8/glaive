using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.TodoLists;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Infrastructure.Persistence;

/// <summary>
/// Tabela todo_list_members – osoby, którym udostępniono listę (także z nieprzyjętym jeszcze zaproszeniem).
/// Usunięcie listy albo użytkownika usuwa wpis (kaskada).
/// </summary>
internal sealed class TodoListMemberConfiguration : IEntityTypeConfiguration<TodoListMember>
{
    public void Configure(EntityTypeBuilder<TodoListMember> builder)
    {
        builder.ToTable("todo_list_members", table =>
            table.HasCheckConstraint("ck_todo_list_members_role", "role IN ('Viewer', 'Editor')"));

        // Klucz główny służy też do wyszukiwania osób na liście (list_id jest pierwszy).
        builder.HasKey(m => new { m.ListId, m.UserId }).HasName("pk_todo_list_members");

        builder.Property(m => m.ListId).HasColumnName("list_id");
        builder.HasOne<TodoList>()
            .WithMany()
            .HasForeignKey(m => m.ListId)
            .HasConstraintName("fk_todo_list_members_todo_lists_list_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(m => m.UserId).HasColumnName("user_id");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .HasConstraintName("fk_todo_list_members_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Listy udostępnione użytkownikowi i jego zaproszenia.
        builder.HasIndex(m => m.UserId).HasDatabaseName("ix_todo_list_members_user_id");

        // Enum jako tekst – czytelny w bazie i odporny na zmianę kolejności wartości w kodzie.
        builder.Property(m => m.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20);

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.AcceptedAt).HasColumnName("accepted_at");
    }
}
