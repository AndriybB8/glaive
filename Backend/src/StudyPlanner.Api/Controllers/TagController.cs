using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.Tags;

namespace StudyPlanner.Api.Controllers;

[ApiController]
[Route("api/tags")]
public class TagController(TagService tagService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> GetBySchedule(Guid scheduleId, CancellationToken cancellationToken)
    {
        var res = await tagService.GetByScheduleAsync(scheduleId, cancellationToken);
        return res.IsSuccess ? Ok(res.Value) : ToErrorResponse(res.Error!);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TagDto>> GetById(Guid scheduleId, Guid id, CancellationToken cancellationToken)
    {
        var res = await tagService.GetByIdAsync(scheduleId, id, cancellationToken);
        return res.IsSuccess ? Ok(res.Value) : ToErrorResponse(res.Error!);
    }

    [HttpPost]
    public async Task<ActionResult<TagDto>> Create(Guid scheduleId, CreateTagRequest req, CancellationToken cancellationToken)
    {
        var res = await tagService.CreateAsync(scheduleId, req, cancellationToken);
        if (!res.IsSuccess)
        {
            return ToErrorResponse(res.Error!);
        }
        return CreatedAtAction(nameof(GetById), new { scheduleId, id = res.Value!.Id }, res.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TagDto>> Update(Guid scheduleId, Guid id, UpdateTagRequest req,
        CancellationToken cancellationToken)
    {
        var res = await tagService.UpdateAsync(scheduleId, id, req, cancellationToken);
        return res.IsSuccess ? Ok(res.Value) : ToErrorResponse(res.Error!);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid scheduleId, Guid id, CancellationToken cancellationToken)
    {
        var result = await tagService.DeleteAsync(scheduleId, id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }


    private ObjectResult ToErrorResponse(TagError error)
    {
        var status = error.Type switch
        {
            TagErrorType.NotFound => StatusCodes.Status404NotFound,
            TagErrorType.Conflict => StatusCodes.Status409Conflict,
            TagErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = new ProblemDetails { Status = status, Detail = error.Message };
        if (error.Current is not null)
        {
            problem.Extensions["current"] = error.Current;
        }

        return new ObjectResult(problem) { StatusCode = status };
    }
}
