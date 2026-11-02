using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.Pomodoro;

namespace StudyPlanner.Api.Controllers;

/// <summary>Ustawienia timera Pomodoro zalogowanego użytkownika (czasy faz i liczba sesji w cyklu).</summary>
[ApiController]
[Route("api/pomodoro-settings")]
public sealed class PomodoroSettingsController(PomodoroSettingsService pomodoroSettingsService) : ControllerBase
{
    /// <summary>Zapisane ustawienia albo domyślne (25/5/15, 4 sesje) z <c>isDefault: true</c>.</summary>
    [HttpGet]
    public async Task<ActionResult<PomodoroSettingsDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await pomodoroSettingsService.GetAsync(cancellationToken));
    }

    /// <summary>Zapisuje pełny zestaw ustawień – przy pierwszym zapisie je tworzy.</summary>
    [HttpPut]
    public async Task<ActionResult<PomodoroSettingsDto>> Update(UpdatePomodoroSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await pomodoroSettingsService.UpdateAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    private static ObjectResult ToErrorResponse(PomodoroSettingsError error)
    {
        var problem = new ProblemDetails { Status = StatusCodes.Status400BadRequest, Detail = error.Message };
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
    }
}
