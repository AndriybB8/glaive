using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.Calendar;

namespace StudyPlanner.Api.Controllers;

[ApiController]
[Route("api/calendar-events")]
public sealed class CalendarEventsController(CalendarEventService calendarEventService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CalendarEventDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await calendarEventService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpPost]
    public async Task<ActionResult<CalendarEventDto>> Create(CreateCalendarEventRequest request, CancellationToken cancellationToken)
    {
        var result = await calendarEventService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Error!);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Dla serii: "scope" i "occurrenceDate" w treści żądania (patrz <see cref="UpdateCalendarEventRequest"/>).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CalendarEventDto>> Update(Guid id, UpdateCalendarEventRequest request, CancellationToken cancellationToken)
    {
        var result = await calendarEventService.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    /// <summary>Dla serii: DELETE api/calendar-events/{id}?scope=single&amp;occurrenceDate=2026-11-30 (albo following / all).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] CalendarEditScope? scope,
        [FromQuery] DateOnly? occurrenceDate,
        CancellationToken cancellationToken)
    {
        var result = await calendarEventService.DeleteAsync(id, scope, occurrenceDate, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    private ObjectResult ToErrorResponse(CalendarError error)
    {
        var status = error.Type switch
        {
            CalendarErrorType.NotFound => StatusCodes.Status404NotFound,
            CalendarErrorType.Conflict => StatusCodes.Status409Conflict,
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
