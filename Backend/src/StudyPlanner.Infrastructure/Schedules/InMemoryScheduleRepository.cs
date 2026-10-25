using System.Collections.Concurrent;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Domain.Schedules;

namespace StudyPlanner.Infrastructure.Schedules;

/// <summary>
/// TYMCZASOWE repozytorium w pamięci, żeby API i frontend działały przed podłączeniem bazy.
/// Dane znikają po restarcie backendu. Do zastąpienia implementacją opartą na EF Core
/// (tabela schedules z kluczem obcym user_id → users; rejestracja w Program.cs).
/// </summary>
public sealed class InMemoryScheduleRepository : IScheduleRepository
{
    private readonly ConcurrentDictionary<Guid, Schedule> _schedules = new();

    public Task<IReadOnlyList<Schedule>> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Schedule> result = _schedules.Values.Where(s => s.UserId == userId).ToList();
        return Task.FromResult(result);
    }

    public Task<Schedule?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var schedule = _schedules.TryGetValue(id, out var found) && found.UserId == userId ? found : null;
        return Task.FromResult(schedule);
    }

    public Task AddAsync(Schedule schedule, CancellationToken cancellationToken)
    {
        _schedules[schedule.Id] = schedule;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Schedule schedule, CancellationToken cancellationToken)
    {
        _schedules[schedule.Id] = schedule;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Schedule schedule, CancellationToken cancellationToken)
    {
        _schedules.TryRemove(schedule.Id, out _);
        return Task.CompletedTask;
    }
}
