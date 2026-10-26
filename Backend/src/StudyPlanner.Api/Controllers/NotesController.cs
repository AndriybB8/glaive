using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.Notes;

namespace StudyPlanner.Api.Controllers;

[ApiController]
[Route("api/notes")]
public sealed class NotesController(NoteService noteService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NoteSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await noteService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoteDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await noteService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpPost]
    public async Task<ActionResult<NoteDto>> Create(CreateNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await noteService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Error!);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Zapis zmian – wywoływany także przez autozapis na froncie.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NoteDto>> Update(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await noteService.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    /// <summary>
    /// Przeniesienie notatki do grupy (<c>{ "groupId": null }</c> = „Bez grupy”).
    /// Nie zmienia wersji notatki, więc nie przerywa autozapisu w otwartym edytorze.
    /// </summary>
    [HttpPut("{id:guid}/group")]
    public async Task<IActionResult> MoveToGroup(Guid id, MoveNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await noteService.MoveToGroupAsync(id, request, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await noteService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    private ObjectResult ToErrorResponse(NoteError error)
    {
        var status = error.Type switch
        {
            NoteErrorType.NotFound => StatusCodes.Status404NotFound,
            NoteErrorType.Conflict => StatusCodes.Status409Conflict,
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
