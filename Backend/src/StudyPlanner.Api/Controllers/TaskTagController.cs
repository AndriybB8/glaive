using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.Tags;
using StudyPlanner.Application.TaskTags;

namespace StudyPlanner.Api.Controllers;

[ApiController]
[Route("api/tasks/{taskId:guid}/tags")]
public class TaskTagsController(TaskTagService ttService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> GetAll(Guid taskId, CancellationToken cancellationToken)
    {
        var res = await ttService.GetTagsForTaskAsync(taskId, cancellationToken);
        return res.IsSuccess ? Ok(res.Value!.Select(TagDto.From).ToList()) : ToErrorResponse(res.Error!);
    }

    [HttpPost("{tagId:guid}")]
    public async Task<IActionResult> Assign(Guid taskId, Guid tagId, CancellationToken cancellationToken)
    {
        var res = await ttService.AssignAsync(taskId, tagId, cancellationToken);
        return res.IsSuccess ? NoContent() : ToErrorResponse(res.Error!);
    }

    [HttpDelete("{tagId:guid}")]
    public async Task<IActionResult> Unassign(Guid taskId, Guid tagId, CancellationToken cancellationToken)
    {
        var res = await ttService.UnassignAsync(taskId, tagId, cancellationToken);
        return res.IsSuccess ? NoContent() : ToErrorResponse(res.Error!);
    }


    private static ObjectResult ToErrorResponse(TaskTagError error)
    {
        var status = error.Type switch
        {
            TaskTagErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status404NotFound,
        };

        var problem = new ProblemDetails { Status = status, Detail = error.Message };
        return new ObjectResult(problem) { StatusCode = status };
    }
}
