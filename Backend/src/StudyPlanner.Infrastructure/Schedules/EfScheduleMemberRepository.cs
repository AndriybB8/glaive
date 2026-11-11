using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.Schedules;

public sealed class EfScheduleMemberRepository(AppDbContext db) : IScheduleMemberRepository
{
    public async Task<IReadOnlyList<ScheduleMemberWithEmail>> GetByScheduleAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        return await db.ScheduleMembers
            .AsNoTracking()
            .Where(m => m.ScheduleId == scheduleId)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new ScheduleMemberWithEmail(m, u.Email))
            .ToListAsync(cancellationToken);
    }

    public Task<ScheduleMemberWithEmail?> GetAsync(Guid scheduleId, Guid userId, CancellationToken cancellationToken)
    {
        return db.ScheduleMembers
            .Where(m => m.ScheduleId == scheduleId && m.UserId == userId)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new ScheduleMemberWithEmail(m, u.Email))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScheduleInvitationDto>> GetPendingByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await (
            from member in db.ScheduleMembers
            where member.UserId == userId && member.AcceptedAt == null
            join schedule in db.Schedules on member.ScheduleId equals schedule.Id
            join owner in db.Users on schedule.UserId equals owner.Id
            select new ScheduleInvitationDto(schedule.Id, schedule.Name, owner.Email, member.Role, member.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AddScheduleMemberResult> AddAsync(ScheduleMember member, CancellationToken cancellationToken)
    {
        db.ScheduleMembers.Add(member);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return AddScheduleMemberResult.Added;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return AddScheduleMemberResult.AlreadyExists;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            db.ChangeTracker.Clear();
            return AddScheduleMemberResult.ReferenceMissing;
        }
    }

    public async Task<bool> UpdateAsync(ScheduleMember member, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<bool> RemoveAsync(Guid scheduleId, Guid userId, CancellationToken cancellationToken)
    {
        var removed = await db.ScheduleMembers
            .Where(m => m.ScheduleId == scheduleId && m.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        return removed > 0;
    }
}
