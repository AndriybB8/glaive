using Microsoft.EntityFrameworkCore;
using StudyPlanner.Domain.NoteGroups;
using StudyPlanner.Domain.Notes;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Domain.Tasks;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Infrastructure.Persistence;

/// <summary>
/// Kontekst bazy PostgreSQL. Mapowanie tabel jest w klasach *Configuration w tym folderze.
/// Kolejne tabele (users…) dodajemy jako DbSet + konfiguracja + migracja.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();

    public DbSet<NoteGroup> NoteGroups => Set<NoteGroup>();

    public DbSet<Schedule> Schedules => Set<Schedule>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
