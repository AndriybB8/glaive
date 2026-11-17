using Microsoft.EntityFrameworkCore;
using StudyPlanner.Application.Files;
using StudyPlanner.Domain.Files;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.Files;

public class EfUploadedFileRepository(AppDbContext db) : IUploadedFileRepository
{
    public async Task<IReadOnlyList<UploadedFile>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        return await db.Files
            .AsNoTracking()
            .Where(f => f.OwnerId == ownerId)
            .ToListAsync(cancellationToken);
    }

    public Task<UploadedFile?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        return db.Files.FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId, cancellationToken);
    }

    public Task<UploadedFile?> GetByOwnerAndHashAsync(Guid ownerId, string contentHash, CancellationToken cancellationToken)
    {
        return db.Files
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.OwnerId == ownerId && f.ContentHash == contentHash, cancellationToken);
    }

    public async Task AddAsync(UploadedFile file, CancellationToken cancellationToken)
    {
        db.Files.Add(file);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UploadedFile file, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new FileConcurrencyException();
        }
    }

    public async Task DeleteAsync(UploadedFile file, CancellationToken cancellationToken)
    {
        db.Files.Remove(file);
        await db.SaveChangesAsync(cancellationToken);
    }
}
