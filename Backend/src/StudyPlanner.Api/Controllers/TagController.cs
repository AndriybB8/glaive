using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.Tags;

namespace StudyPlanner.Api.Controllers;

[ApiController]
[Route("api/tags")]
public class TagController(TagService tagService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await tagService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TagDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var res = await tagService.GetByIdAsync(id, cancellationToken);
        return res.IsSuccess ? Ok(res.Value) : ToErrorResponse(res.Error!);
    }

    [HttpPost]
    public async Task<ActionResult<TagDto>> Create(CreateTagRequest req, CancellationToken cancellationToken)
    {
        var res = await tagService.CreateAsync(req, cancellationToken);
        if (!res.IsSuccess)
        {
            return ToErrorResponse(res.Error!);
        }
        return CreatedAtAction(nameof(GetById), new { id = res.Value!.Id }, res.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TagDto>> Update(Guid id, UpdateTagRequest req,
        CancellationToken cancellationToken)
    {
        var res = await tagService.UpdateAsync(id, req, cancellationToken);
        return res.IsSuccess ? Ok(res.Value) : ToErrorResponse(res.Error!);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await tagService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }


    private ObjectResult ToErrorResponse(TagError error)
    {
        var status = error.Type switch
        {
            TagErrorType.NotFound => StatusCodes.Status404NotFound,
            TagErrorType.Conflict => StatusCodes.Status409Conflict,
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
