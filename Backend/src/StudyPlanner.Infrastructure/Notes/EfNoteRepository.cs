using Microsoft.EntityFrameworkCore;
using StudyPlanner.Application.Notes;
using StudyPlanner.Domain.Notes;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.Notes;

public sealed class EfNoteRepository(AppDbContext db) : INoteRepository
{
    public async Task<IReadOnlyList<NoteSummaryDto>> GetSummariesByAuthorAsync(Guid authorId, CancellationToken cancellationToken)
    {
        // Projekcja: SELECT bez kolumny content.
        return await db.Notes
            .AsNoTracking()
            .Where(n => n.AuthorId == authorId)
            .Select(n => new NoteSummaryDto(n.Id, n.Title, n.UpdatedAt, n.GroupId))
            .ToListAsync(cancellationToken);
    }

    public Task<Note?> GetByIdAsync(Guid id, Guid authorId, CancellationToken cancellationToken)
    {
        // Śledzona przez kontekst – NoteService zmienia ją i zapisuje przez UpdateAsync.
        return db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.AuthorId == authorId, cancellationToken);
    }

    public async Task AddAsync(Note note, CancellationToken cancellationToken)
    {
        db.Notes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Note note, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Zapominamy nieaktualne dane, żeby kolejny odczyt pobrał wersję z bazy (pokazywaną przy konflikcie).
            db.ChangeTracker.Clear();
            throw new NoteConcurrencyException();
        }
    }

    public async Task<bool> MoveToGroupAsync(Guid id, Guid authorId, Guid? groupId, CancellationToken cancellationToken)
    {
        // Jedno UPDATE … SET group_id bez warunku na version – przeniesienie nie koliduje z autozapisem treści.
        var updated = await db.Notes
            .Where(n => n.Id == id && n.AuthorId == authorId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.GroupId, groupId), cancellationToken);

        return updated > 0;
    }

    public async Task DeleteAsync(Note note, CancellationToken cancellationToken)
    {
        db.Notes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);
    }
}
