using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPlanner.Application.TaskTags;
using StudyPlanner.Domain.Tags;
using StudyPlanner.Domain.TaskTags;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.TaskTags;

public class EfTaskTagRepository(AppDbContext dbContext) : ITaskTagRepository
{
    public async Task<IReadOnlyList<Tag>> GetTagsForTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        return await dbContext.TaskTags
            .Where(tt => tt.TaskId == taskId)
            .Join(dbContext.Tags, tt=>tt.TagId, t => t.Id, (tt, t)=> t)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken) =>
        dbContext.TaskTags.AnyAsync(tt => tt.TaskId == taskId && tt.TagId == tagId, cancellationToken);

    public async Task AssignAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken)
    {
        dbContext.TaskTags.Add(new TaskTag(taskId, tagId));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.ChangeTracker.Clear(); //Someone already did this just now, disregard the changes
        }
    }

    public async Task UnassignAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.TaskTags
            .FirstOrDefaultAsync(tt => tt.TaskId == taskId && tt.TagId == tagId, cancellationToken);
        if (entity is null) return;

        dbContext.TaskTags.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
