using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.Calendar;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Infrastructure.Persistence;

/// <summary>
/// Tabela calendar_events – własne wydarzenia użytkownika: jednorazowe, serie i wyjątki serii (zmienione albo odwołane wystąpienia).
/// Usunięcie użytkownika usuwa jego wydarzenia, a usunięcie serii – jej wyjątki (kaskada).
/// </summary>
internal sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("calendar_events", table =>
        {
            // Dokładnie jedna para: godziny (UTC) albo dni. Pozostałe sprawdzenia przepuszczają NULL, więc kształt pilnuje ten warunek.
            table.HasCheckConstraint(
                "ck_calendar_events_time_shape",
                "(is_all_day AND start_date IS NOT NULL AND end_date IS NOT NULL AND starts_at IS NULL AND ends_at IS NULL)"
                + " OR (NOT is_all_day AND starts_at IS NOT NULL AND ends_at IS NOT NULL AND start_date IS NULL AND end_date IS NULL)");
            table.HasCheckConstraint("ck_calendar_events_date_range", "end_date >= start_date");
            table.HasCheckConstraint("ck_calendar_events_time_range", "ends_at > starts_at");
            table.HasCheckConstraint("ck_calendar_events_color", "color ~ '^#[0-9A-Fa-f]{6}$'");
            table.HasCheckConstraint(
                "ck_calendar_events_recurrence_frequency",
                "recurrence_frequency IN ('None', 'Daily', 'Weekly', 'Monthly')");
            table.HasCheckConstraint(
                "ck_calendar_events_recurrence_until",
                "recurrence_until IS NULL OR recurrence_frequency <> 'None'");

            // Wyjątek wskazuje serię i dzień wystąpienia i sam się nie powtarza; tylko wyjątek może być odwołany.
            table.HasCheckConstraint(
                "ck_calendar_events_exception_shape",
                "(series_id IS NULL AND original_date IS NULL AND NOT is_cancelled)"
                + " OR (series_id IS NOT NULL AND original_date IS NOT NULL AND recurrence_frequency = 'None')");
        });

        builder.HasKey(e => e.Id).HasName("pk_calendar_events");
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .HasConstraintName("fk_calendar_events_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.UserId).HasDatabaseName("ix_calendar_events_user_id");

        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(CalendarEvent.TitleMaxLength).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(CalendarEvent.DescriptionMaxLength);
        builder.Property(e => e.Color).HasColumnName("color").HasMaxLength(CalendarEvent.ColorLength).IsRequired();

        builder.Property(e => e.IsAllDay).HasColumnName("is_all_day");
        builder.Property(e => e.StartsAt).HasColumnName("starts_at");
        builder.Property(e => e.EndsAt).HasColumnName("ends_at");
        builder.Property(e => e.StartDate).HasColumnName("start_date");
        builder.Property(e => e.EndDate).HasColumnName("end_date");
        builder.Property(e => e.TimeZone).HasColumnName("time_zone").HasMaxLength(CalendarEvent.TimeZoneMaxLength).IsRequired();

        // Enum jako tekst – czytelny w bazie i odporny na zmianę kolejności wartości w kodzie.
        builder.Property(e => e.Recurrence).HasColumnName("recurrence_frequency").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.RecurrenceUntil).HasColumnName("recurrence_until");

        builder.Property(e => e.SeriesId).HasColumnName("series_id");
        builder.HasOne<CalendarEvent>()
            .WithMany()
            .HasForeignKey(e => e.SeriesId)
            .HasConstraintName("fk_calendar_events_calendar_events_series_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.OriginalDate).HasColumnName("original_date");
        builder.Property(e => e.IsCancelled).HasColumnName("is_cancelled");

        // Jedno wystąpienie serii ma co najwyżej jeden wyjątek. NULL-e są różne, więc wydarzenia bez serii się nie blokują.
        // Indeks zaczyna się od series_id, więc służy też kluczowi obcemu.
        builder.HasIndex(e => new { e.SeriesId, e.OriginalDate })
            .IsUnique()
            .HasDatabaseName("ux_calendar_events_series_id_original_date");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // Zapis sprawdza, czy wersja w bazie się nie zmieniła – stąd 409 przy edycji w dwóch kartach.
        builder.Property(e => e.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
