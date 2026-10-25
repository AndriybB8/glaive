using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.Tasks;

namespace StudyPlanner.Api.Controllers;

/// <summary>Lista i dodawanie pod harmonogramem, pozostałe operacje bezpośrednio na zadaniu.</summary>
[ApiController]
public sealed class TasksController(TaskService taskService) : ControllerBase
{
    [HttpGet("api/schedules/{scheduleId:guid}/tasks")]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetBySchedule(Guid scheduleId, CancellationToken cancellationToken)
    {
        var result = await taskService.GetByScheduleAsync(scheduleId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpPost("api/schedules/{scheduleId:guid}/tasks")]
    public async Task<ActionResult<TaskDto>> Create(Guid scheduleId, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await taskService.CreateAsync(scheduleId, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Error!);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("api/tasks/{id:guid}")]
    public async Task<ActionResult<TaskDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await taskService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpPut("api/tasks/{id:guid}")]
    public async Task<ActionResult<TaskDto>> Update(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await taskService.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    /// <summary>Zmiana samego statusu (do zrobienia / w trakcie / zrobione) – z listy zadań.</summary>
    [HttpPatch("api/tasks/{id:guid}/status")]
    public async Task<ActionResult<TaskDto>> ChangeStatus(Guid id, ChangeTaskStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await taskService.ChangeStatusAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpDelete("api/tasks/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await taskService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    private ObjectResult ToErrorResponse(TaskError error)
    {
        var status = error.Type switch
        {
            TaskErrorType.NotFound => StatusCodes.Status404NotFound,
            TaskErrorType.Conflict => StatusCodes.Status409Conflict,
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
