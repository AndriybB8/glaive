using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.Schedules;

namespace StudyPlanner.Api.Controllers;

[ApiController]
[Route("api/schedules")]
public sealed class SchedulesController(ScheduleService scheduleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScheduleDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await scheduleService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScheduleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await scheduleService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpPost]
    public async Task<ActionResult<ScheduleDto>> Create(CreateScheduleRequest request, CancellationToken cancellationToken)
    {
        var result = await scheduleService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Error!);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ScheduleDto>> Update(Guid id, UpdateScheduleRequest request, CancellationToken cancellationToken)
    {
        var result = await scheduleService.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await scheduleService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    private ObjectResult ToErrorResponse(ScheduleError error)
    {
        var status = error.Type switch
        {
            ScheduleErrorType.NotFound => StatusCodes.Status404NotFound,
            ScheduleErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = new ProblemDetails { Status = status, Detail = error.Message };
        if (error.Current is not null)
        {
            // Frontend używa tego, żeby pokazać aktualną wersję przy konflikcie.
            problem.Extensions["current"] = error.Current;
        }

        return new ObjectResult(problem) { StatusCode = status };
    }
}
