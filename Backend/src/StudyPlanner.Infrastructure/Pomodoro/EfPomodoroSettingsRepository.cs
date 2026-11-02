using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPlanner.Application.Pomodoro;
using StudyPlanner.Domain.Pomodoro;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.Pomodoro;

public sealed class EfPomodoroSettingsRepository(AppDbContext db) : IPomodoroSettingsRepository
{
    public Task<PomodoroSettings?> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Śledzone przez kontekst – PomodoroSettingsService zmienia je i zapisuje przez UpdateAsync.
        return db.PomodoroSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(PomodoroSettings settings, CancellationToken cancellationToken)
    {
        db.PomodoroSettings.Add(settings);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Zapominamy odrzucony wiersz, żeby kolejny odczyt pobrał ten zapisany w bazie przez inną kartę.
            db.ChangeTracker.Clear();
            throw new PomodoroSettingsAlreadyExistsException();
        }
    }

    public async Task UpdateAsync(PomodoroSettings settings, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
    }
}
