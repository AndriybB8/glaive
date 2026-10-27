using Microsoft.EntityFrameworkCore;
using StudyPlanner.Domain.NoteGroups;
using StudyPlanner.Domain.Notes;
using StudyPlanner.Domain.NoteTasks;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.Infrastructure.Persistence;

/// <summary>
/// Kontekst bazy PostgreSQL. Mapowanie tabel jest w klasach *Configuration w tym folderze.
/// Kolejne tabele (users…) dodajemy jako DbSet + konfiguracja + migracja.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();

    public DbSet<NoteGroup> NoteGroups => Set<NoteGroup>();

    /// <summary>Powiązania notatek z zadaniami (tabela note_tasks).</summary>
    public DbSet<NoteTaskLink> NoteTaskLinks => Set<NoteTaskLink>();

    public DbSet<Schedule> Schedules => Set<Schedule>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
