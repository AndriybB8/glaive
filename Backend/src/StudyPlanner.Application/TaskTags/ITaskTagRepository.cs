using StudyPlanner.Domain.Tags;

namespace StudyPlanner.Application.TaskTags;

public interface ITaskTagRepository
{
    Task<IReadOnlyList<Tag>> GetTagsForTaskAsync(Guid taskId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken);
    Task AssignAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken);
    Task UnassignAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken);
}
