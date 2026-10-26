using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.NoteGroups;
using StudyPlanner.Domain.Notes;

namespace StudyPlanner.Infrastructure.Persistence;

/// <summary>Tabela notes. Nazwy tabel i kolumn w snake_case, jak przyjęto w PostgreSQL.</summary>
internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes");

        builder.HasKey(n => n.Id).HasName("pk_notes");
        builder.Property(n => n.Id).HasColumnName("id").ValueGeneratedNever();

        // TODO: klucz obcy do users(id), gdy powstanie tabela użytkowników (logowanie).
        builder.Property(n => n.AuthorId).HasColumnName("author_id");
        builder.HasIndex(n => n.AuthorId).HasDatabaseName("ix_notes_author_id");

        // Usunięcie grupy nie usuwa notatek – baza ustawia im group_id = NULL („Bez grupy”).
        builder.Property(n => n.GroupId).HasColumnName("group_id");
        builder.HasOne<NoteGroup>()
            .WithMany()
            .HasForeignKey(n => n.GroupId)
            .HasConstraintName("fk_notes_note_groups_group_id")
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(n => n.GroupId).HasDatabaseName("ix_notes_group_id");

        // Pusty tytuł jest dozwolony („Bez tytułu” na froncie), ale nigdy NULL.
        builder.Property(n => n.Title).HasColumnName("title").HasMaxLength(Note.TitleMaxLength).IsRequired();

        // HTML z edytora bywa długi, więc typ text. Limit długości pilnuje domena (Note.ContentMaxLength).
        builder.Property(n => n.Content).HasColumnName("content").HasColumnType("text").IsRequired();

        builder.Property(n => n.CreatedAt).HasColumnName("created_at");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at");

        // Zapis sprawdza, czy wersja w bazie się nie zmieniła (WHERE version = …) – stąd 409 przy edycji w dwóch kartach.
        builder.Property(n => n.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
