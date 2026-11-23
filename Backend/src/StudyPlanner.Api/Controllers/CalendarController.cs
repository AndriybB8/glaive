using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.Calendar;

namespace StudyPlanner.Api.Controllers;

[ApiController]
[Route("api/calendar")]
public sealed class CalendarController(CalendarService calendarService) : ControllerBase
{
    /// <summary>
    /// Elementy kalendarza w widocznym zakresie, np. GET api/calendar?from=2026-11-23&amp;to=2026-11-29&amp;timeZone=Europe/Warsaw.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CalendarItemDto>>> Get(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? timeZone,
        CancellationToken cancellationToken)
    {
        var result = await calendarService.GetAsync(from, to, timeZone, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    private ObjectResult ToErrorResponse(CalendarError error)
    {
        var status = error.Type switch
        {
            CalendarErrorType.NotFound => StatusCodes.Status404NotFound,
            CalendarErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return new ObjectResult(new ProblemDetails { Status = status, Detail = error.Message }) { StatusCode = status };
    }
}
