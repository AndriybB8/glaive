using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.NoteGroups;

namespace StudyPlanner.Api.Controllers;

/// <summary>Grupy (foldery) notatek. Przeniesienie notatki do grupy: PUT /api/notes/{id}/group.</summary>
[ApiController]
[Route("api/note-groups")]
public sealed class NoteGroupsController(NoteGroupService noteGroupService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NoteGroupDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await noteGroupService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoteGroupDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await noteGroupService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpPost]
    public async Task<ActionResult<NoteGroupDto>> Create(CreateNoteGroupRequest request, CancellationToken cancellationToken)
    {
        var result = await noteGroupService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Error!);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Zmiana nazwy grupy.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NoteGroupDto>> Update(Guid id, UpdateNoteGroupRequest request, CancellationToken cancellationToken)
    {
        var result = await noteGroupService.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    /// <summary>Usuwa grupę. Jej notatki trafiają do „Bez grupy”.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await noteGroupService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    private ObjectResult ToErrorResponse(NoteGroupError error)
    {
        var status = error.Type switch
        {
            NoteGroupErrorType.NotFound => StatusCodes.Status404NotFound,
            NoteGroupErrorType.Conflict => StatusCodes.Status409Conflict,
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
