using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.Schedules;

namespace StudyPlanner.Infrastructure.Persistence;

/// <summary>Tabela schedules. Nazwy tabel i kolumn w snake_case, jak przyjęto w PostgreSQL.</summary>
internal sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.ToTable("schedules", table =>
            table.HasCheckConstraint("ck_schedules_date_range", "end_date >= start_date"));

        builder.HasKey(s => s.Id).HasName("pk_schedules");
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        // TODO: klucz obcy do users(id), gdy powstanie tabela użytkowników (logowanie).
        builder.Property(s => s.UserId).HasColumnName("user_id");
        builder.HasIndex(s => s.UserId).HasDatabaseName("ix_schedules_user_id");

        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(Schedule.NameMaxLength).IsRequired();
        builder.Property(s => s.StartDate).HasColumnName("start_date");
        builder.Property(s => s.EndDate).HasColumnName("end_date");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        // Zapis sprawdza, czy wersja w bazie się nie zmieniła (WHERE version = …) – stąd 409 przy edycji w dwóch kartach.
        builder.Property(s => s.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
