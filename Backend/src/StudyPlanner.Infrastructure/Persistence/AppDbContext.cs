using Microsoft.EntityFrameworkCore;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.Infrastructure.Persistence;

/// <summary>
/// Kontekst bazy PostgreSQL. Mapowanie tabel jest w klasach *Configuration w tym folderze.
/// Kolejne tabele (users, notes, tasks…) dodajemy jako DbSet + konfiguracja + migracja.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Schedule> Schedules => Set<Schedule>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
