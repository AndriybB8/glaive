using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.Notes;
using StudyPlanner.Application.NoteTasks;

namespace StudyPlanner.Api.Controllers;

/// <summary>
/// Przypisywanie notatek do zadań. Powiązanie to zasób pod notatką:
/// PUT przypisuje (można powtarzać), DELETE odłącza.
/// </summary>
[ApiController]
public sealed class NoteTasksController(NoteTaskService noteTaskService) : ControllerBase
{
    /// <summary>Zadania przypisane do notatki.</summary>
    [HttpGet("api/notes/{noteId:guid}/tasks")]
    public async Task<ActionResult<IReadOnlyList<LinkedTaskDto>>> GetTasksForNote(Guid noteId, CancellationToken cancellationToken)
    {
        var result = await noteTaskService.GetTasksForNoteAsync(noteId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    /// <summary>Notatki przypisane do zadania (bez treści).</summary>
    [HttpGet("api/tasks/{taskId:guid}/notes")]
    public async Task<ActionResult<IReadOnlyList<NoteSummaryDto>>> GetNotesForTask(Guid taskId, CancellationToken cancellationToken)
    {
        var result = await noteTaskService.GetNotesForTaskAsync(taskId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    /// <summary>Przypisuje notatkę do zadania. Zwraca przypisane zadanie (do pokazania przy notatce).</summary>
    [HttpPut("api/notes/{noteId:guid}/tasks/{taskId:guid}")]
    public async Task<ActionResult<LinkedTaskDto>> Link(Guid noteId, Guid taskId, CancellationToken cancellationToken)
    {
        var result = await noteTaskService.LinkAsync(noteId, taskId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    /// <summary>Odłącza notatkę od zadania.</summary>
    [HttpDelete("api/notes/{noteId:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> Unlink(Guid noteId, Guid taskId, CancellationToken cancellationToken)
    {
        var result = await noteTaskService.UnlinkAsync(noteId, taskId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    private ObjectResult ToErrorResponse(NoteTaskError error)
    {
        var status = error.Type switch
        {
            NoteTaskErrorType.NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest,
        };

        return new ObjectResult(new ProblemDetails { Status = status, Detail = error.Message }) { StatusCode = status };
    }
}
