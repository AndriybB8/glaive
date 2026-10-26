using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPlanner.Application.NoteGroups;
using StudyPlanner.Domain.NoteGroups;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.NoteGroups;

public sealed class EfNoteGroupRepository(AppDbContext db) : INoteGroupRepository
{
    public async Task<IReadOnlyList<NoteGroup>> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.NoteGroups
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task<NoteGroup?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        // Śledzona przez kontekst – NoteGroupService zmienia ją i zapisuje przez UpdateAsync.
        return db.NoteGroups.FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        return db.NoteGroups.AnyAsync(g => g.Id == id && g.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(NoteGroup group, CancellationToken cancellationToken)
    {
        db.NoteGroups.Add(group);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueNameViolation(ex))
        {
            db.ChangeTracker.Clear();
            throw new NoteGroupNameTakenException();
        }
    }

    public async Task UpdateAsync(NoteGroup group, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Zapominamy nieaktualne dane, żeby kolejny odczyt pobrał wersję z bazy (pokazywaną przy konflikcie).
            db.ChangeTracker.Clear();
            throw new NoteGroupConcurrencyException();
        }
        catch (DbUpdateException ex) when (IsUniqueNameViolation(ex))
        {
            db.ChangeTracker.Clear();
            throw new NoteGroupNameTakenException();
        }
    }

    public async Task DeleteAsync(NoteGroup group, CancellationToken cancellationToken)
    {
        // Notatki z tej grupy dostają group_id = NULL po stronie bazy (ON DELETE SET NULL).
        db.NoteGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueNameViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
