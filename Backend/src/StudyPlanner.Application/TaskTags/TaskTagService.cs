using StudyPlanner.Application.Common;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Application.Tags;
using StudyPlanner.Application.Tasks;
using StudyPlanner.Domain.Tags;
using StudyPlanner.Domain.Tasks;

namespace StudyPlanner.Application.TaskTags;

public class TaskTagService(ITaskTagRepository taskTags, ITaskRepository tasks, ITagRepository tags, IScheduleRepository schedules, ICurrentUser currentUser)
{
    public async Task<TaskTagResult<IReadOnlyList<Tag>>> GetTagsForTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        if (await FindTaskAsync(taskId, cancellationToken) is null)
            return TaskTagResult<IReadOnlyList<Tag>>.Failure(new TaskTagError(TaskTagErrorType.NotFound, "Nie znaleziono zadania."));

        var result = await taskTags.GetTagsForTaskAsync(taskId, cancellationToken);
        return TaskTagResult<IReadOnlyList<Tag>>.Success(result);
    }

    public async Task<TaskTagResult<bool>> AssignAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken)
    {
        var found = await FindTaskAsync(taskId, cancellationToken);
        if (found is null) return TaskTagResult<bool>.Failure(new TaskTagError(TaskTagErrorType.NotFound, "Nie znaleziono zadania."));
        if (!found.Value.Access.CanEditTasks()) return TaskTagResult<bool>.Failure(new (TaskTagErrorType.Forbidden, "Masz tylko podgląd tego harmonogramu."));

        var tag = await tags.GetByIdAsync(tagId, currentUser.UserId, cancellationToken);
        if (tag is null)
            return TaskTagResult<bool>.Failure(new(TaskTagErrorType.NotFound, "Nie znaleziono tagu."));

        if (!await taskTags.ExistsAsync(taskId, tagId, cancellationToken))
            await taskTags.AssignAsync(taskId, tagId, cancellationToken);

        return TaskTagResult<bool>.Success(true);
    }

    public async Task<TaskTagResult<bool>> UnassignAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken)
    {
        var found = await FindTaskAsync(taskId, cancellationToken);
        if (found is null) return TaskTagResult<bool>.Failure(new TaskTagError(TaskTagErrorType.NotFound, "Nie znaleziono zadania."));
        if (!found.Value.Access.CanEditTasks()) return TaskTagResult<bool>.Failure(new TaskTagError(TaskTagErrorType.Forbidden, "Masz tylko podgląd tego harmonogramu."));

        var tag = await tags.GetByIdAsync(tagId, currentUser.UserId, cancellationToken);
        if (tag is null) return TaskTagResult<bool>.Failure(new TaskTagError(TaskTagErrorType.NotFound, "Nie znaleziono tagu."));

        await taskTags.UnassignAsync(taskId, tagId, cancellationToken);
        return TaskTagResult<bool>.Success(true);
    }

    private async Task<(TaskItem Task, ScheduleAccess Access)?> FindTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(taskId, cancellationToken);
        if (task is null) return null;

        var scheduleWithAccess = await schedules.GetWithAccessAsync(task.ScheduleId, currentUser.UserId, cancellationToken);
        return scheduleWithAccess is null ? null : (task, scheduleWithAccess.Access);
    }
}
