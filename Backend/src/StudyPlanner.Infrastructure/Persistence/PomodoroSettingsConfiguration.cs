using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.Pomodoro;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Infrastructure.Persistence;

/// <summary>
/// Tabela pomodoro_settings – ustawienia timera Pomodoro. Jeden wiersz na użytkownika, więc kluczem głównym jest user_id.
/// Wiersz powstaje dopiero przy pierwszym zapisie – wcześniej API zwraca wartości domyślne.
/// </summary>
internal sealed class PomodoroSettingsConfiguration : IEntityTypeConfiguration<PomodoroSettings>
{
    public void Configure(EntityTypeBuilder<PomodoroSettings> builder)
    {
        builder.ToTable("pomodoro_settings", table =>
        {
            // Te same zakresy, które sprawdza encja – zabezpieczenie przed zapisem z pominięciem aplikacji.
            table.HasCheckConstraint(
                "ck_pomodoro_settings_work_minutes",
                $"work_minutes BETWEEN {PomodoroSettings.MinMinutes} AND {PomodoroSettings.MaxWorkMinutes}");
            table.HasCheckConstraint(
                "ck_pomodoro_settings_short_break_minutes",
                $"short_break_minutes BETWEEN {PomodoroSettings.MinMinutes} AND {PomodoroSettings.MaxShortBreakMinutes}");
            table.HasCheckConstraint(
                "ck_pomodoro_settings_long_break_minutes",
                $"long_break_minutes BETWEEN {PomodoroSettings.MinMinutes} AND {PomodoroSettings.MaxLongBreakMinutes}");
            table.HasCheckConstraint(
                "ck_pomodoro_settings_sessions_before_long_break",
                $"sessions_before_long_break BETWEEN {PomodoroSettings.MinSessionsBeforeLongBreak} AND {PomodoroSettings.MaxSessionsBeforeLongBreak}");
        });

        builder.HasKey(s => s.UserId).HasName("pk_pomodoro_settings");

        // Usunięcie użytkownika usuwa jego ustawienia.
        builder.Property(s => s.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<PomodoroSettings>(s => s.UserId)
            .HasConstraintName("fk_pomodoro_settings_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.WorkMinutes).HasColumnName("work_minutes");
        builder.Property(s => s.ShortBreakMinutes).HasColumnName("short_break_minutes");
        builder.Property(s => s.LongBreakMinutes).HasColumnName("long_break_minutes");
        builder.Property(s => s.SessionsBeforeLongBreak).HasColumnName("sessions_before_long_break");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
    }
}
