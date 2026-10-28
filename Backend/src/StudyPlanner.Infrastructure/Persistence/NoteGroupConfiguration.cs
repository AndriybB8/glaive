using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.NoteGroups;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Infrastructure.Persistence;

/// <summary>Tabela note_groups – grupy (foldery) notatek. Powiązanie z notatkami jest w NoteConfiguration.</summary>
internal sealed class NoteGroupConfiguration : IEntityTypeConfiguration<NoteGroup>
{
    public void Configure(EntityTypeBuilder<NoteGroup> builder)
    {
        builder.ToTable("note_groups");

        builder.HasKey(g => g.Id).HasName("pk_note_groups");
        builder.Property(g => g.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(g => g.UserId).HasColumnName("user_id");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.UserId)
            .HasConstraintName("fk_note_groups_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(g => g.Name).HasColumnName("name").HasMaxLength(NoteGroup.NameMaxLength).IsRequired();

        // Jedna nazwa na użytkownika. Wielkość liter („Matma” vs „matma”) sprawdza NoteGroupService;
        // indeks jest zabezpieczeniem przy jednoczesnych zapisach. Służy też do wyszukiwania grup użytkownika.
        builder.HasIndex(g => new { g.UserId, g.Name })
            .IsUnique()
            .HasDatabaseName("ux_note_groups_user_id_name");

        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");

        // Zapis sprawdza, czy wersja w bazie się nie zmieniła – stąd 409 przy zmianie nazwy w dwóch kartach.
        builder.Property(g => g.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
