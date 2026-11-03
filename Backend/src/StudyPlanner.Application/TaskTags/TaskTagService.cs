using StudyPlanner.Application.Common;
using StudyPlanner.Application.Tags;
using StudyPlanner.Application.Tasks;
using StudyPlanner.Domain.Tags;

namespace StudyPlanner.Application.TaskTags;

public sealed class TaskTagService(
    ITaskTagRepository taskTags,
    ITaskRepository tasks,
    ITagRepository tags,
    ICurrentUser currentUser)
{
    public async Task<TaskTagResult<IReadOnlyList<Tag>>> GetTagsForTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(taskId, currentUser.UserId, cancellationToken);
        if (task is null)
            return TaskTagResult<IReadOnlyList<Tag>>.Failure(new(TaskTagErrorType.NotFound, "Nie znaleziono zadania."));

        var result = await taskTags.GetTagsForTaskAsync(taskId, cancellationToken);
        return TaskTagResult<IReadOnlyList<Tag>>.Success(result);
    }

    public async Task<TaskTagResult<bool>> AssignAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(taskId, currentUser.UserId, cancellationToken);
        if (task is null)
            return TaskTagResult<bool>.Failure(new(TaskTagErrorType.NotFound, "Nie znaleziono zadania."));

        var tag = await tags.GetByIdAsync(tagId, currentUser.UserId, cancellationToken);
        if (tag is null)
            return TaskTagResult<bool>.Failure(new(TaskTagErrorType.NotFound, "Nie znaleziono tagu."));

        if (!await taskTags.ExistsAsync(taskId, tagId, cancellationToken))
            await taskTags.AssignAsync(taskId, tagId, cancellationToken);

        return TaskTagResult<bool>.Success(true);
    }

    public async Task<TaskTagResult<bool>> UnassignAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(taskId, currentUser.UserId, cancellationToken);
        if (task is null) return TaskTagResult<bool>.Failure(new(TaskTagErrorType.NotFound, "Nie znaleziono zadania."));

        var tag = await tags.GetByIdAsync(tagId, currentUser.UserId, cancellationToken);
        if (tag is null) return TaskTagResult<bool>.Failure(new(TaskTagErrorType.NotFound, "Nie znaleziono tagu."));

        await taskTags.UnassignAsync(taskId, tagId, cancellationToken);
        return TaskTagResult<bool>.Success(true);
    }
}
