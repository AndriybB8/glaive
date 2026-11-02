using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPlanner.Application.Tags;
using StudyPlanner.Domain.Tags;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.Tags;

public class EfTagRepository(AppDbContext db) : ITagRepository
{
    public async Task<IReadOnlyList<Tag>> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.Tags
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task<Tag?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        return db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(Tag tag, CancellationToken cancellationToken)
    {
        db.Tags.Add(tag);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            throw new TagNameTakenException();
        }
    }

    public async Task UpdateAsync(Tag tag, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new TagConcurrencyException();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            throw new TagNameTakenException();
        }
    }

    public async Task DeleteAsync(Tag tag, CancellationToken cancellationToken)
    {
        db.Tags.Remove(tag);
        await db.SaveChangesAsync(cancellationToken);
    }
}
