using Microsoft.EntityFrameworkCore;
using StudyPlanner.Domain.NoteGroups;
using StudyPlanner.Domain.Notes;
using StudyPlanner.Domain.NoteTasks;
using StudyPlanner.Domain.Pomodoro;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Domain.Tags;
using StudyPlanner.Domain.Tasks;
using StudyPlanner.Domain.TaskTags;
using StudyPlanner.Domain.TodoLists;
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

    /// <summary>Powiązania notatek z zadaniami (tabela note_tasks).</summary>
    public DbSet<NoteTaskLink> NoteTaskLinks => Set<NoteTaskLink>();

    public DbSet<Schedule> Schedules => Set<Schedule>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<TodoList> TodoLists => Set<TodoList>();

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    public DbSet<TaskTag> TaskTags => Set<TaskTag>();

    /// <summary>Ustawienia timera Pomodoro – jeden wiersz na użytkownika.</summary>
    public DbSet<PomodoroSettings> PomodoroSettings => Set<PomodoroSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
