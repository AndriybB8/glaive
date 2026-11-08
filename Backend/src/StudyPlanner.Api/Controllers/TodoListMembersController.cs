using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyPlanner.Application.TodoLists;

namespace StudyPlanner.Api.Controllers;

/// <summary>Udostępnianie list: zarządzanie dostępem pod listą, a odpowiedź na zaproszenie pod todo-list-invitations.</summary>
[ApiController]
public sealed class TodoListMembersController(TodoListMemberService memberService) : ControllerBase
{
    /// <summary>Osoby z dostępem i oczekujące zaproszenia – tylko właściciel.</summary>
    [HttpGet("api/todo-lists/{listId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<TodoListMemberDto>>> GetByList(Guid listId, CancellationToken cancellationToken)
    {
        var result = await memberService.GetByListAsync(listId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    /// <summary>
    /// Zaproszenie po adresie email – tylko właściciel. 404, gdy nie ma takiego konta, 409, gdy osoba ma już dostęp lub zaproszenie.
    /// Limit zapytań, bo odpowiedź zdradza, czy adres ma konto.
    /// </summary>
    [HttpPost("api/todo-lists/{listId:guid}/members")]
    [EnableRateLimiting("user-lookup")]
    public async Task<ActionResult<TodoListMemberDto>> Invite(Guid listId, InviteTodoListMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await memberService.InviteAsync(listId, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Error!);
        }

        return Created($"/api/todo-lists/{listId}/members/{result.Value!.UserId}", result.Value);
    }

    /// <summary>Zmiana uprawnień – tylko właściciel.</summary>
    [HttpPut("api/todo-lists/{listId:guid}/members/{userId:guid}")]
    public async Task<ActionResult<TodoListMemberDto>> ChangeRole(
        Guid listId,
        Guid userId,
        ChangeTodoListMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await memberService.ChangeRoleAsync(listId, userId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    /// <summary>Właściciel odbiera dostęp albo cofa zaproszenie; osoba z dostępem podaje swój identyfikator, żeby opuścić listę.</summary>
    [HttpDelete("api/todo-lists/{listId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> Remove(Guid listId, Guid userId, CancellationToken cancellationToken)
    {
        var result = await memberService.RemoveAsync(listId, userId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    /// <summary>Zaproszenia bieżącego użytkownika, na które jeszcze nie odpowiedział.</summary>
    [HttpGet("api/todo-list-invitations")]
    public async Task<ActionResult<IReadOnlyList<TodoListInvitationDto>>> GetInvitations(CancellationToken cancellationToken)
    {
        return Ok(await memberService.GetInvitationsAsync(cancellationToken));
    }

    [HttpPost("api/todo-list-invitations/{listId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid listId, CancellationToken cancellationToken)
    {
        var result = await memberService.AcceptInvitationAsync(listId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    [HttpPost("api/todo-list-invitations/{listId:guid}/decline")]
    public async Task<IActionResult> Decline(Guid listId, CancellationToken cancellationToken)
    {
        var result = await memberService.DeclineInvitationAsync(listId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    private ObjectResult ToErrorResponse(TodoListError error)
    {
        var status = error.Type switch
        {
            TodoListErrorType.NotFound => StatusCodes.Status404NotFound,
            TodoListErrorType.Conflict => StatusCodes.Status409Conflict,
            TodoListErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = new ProblemDetails { Status = status, Detail = error.Message };
        return new ObjectResult(problem) { StatusCode = status };
    }
}
