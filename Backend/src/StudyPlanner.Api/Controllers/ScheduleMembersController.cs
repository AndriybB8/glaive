using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyPlanner.Application.Schedules;

namespace StudyPlanner.Api.Controllers;

[ApiController]
public sealed class ScheduleMembersController(ScheduleMemberService memberService) : ControllerBase
{
    [HttpGet("api/schedules/{scheduleId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<ScheduleMemberDto>>> GetBySchedule(Guid scheduleId, CancellationToken cancellationToken)
    {
        var result = await memberService.GetByScheduleAsync(scheduleId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpPost("api/schedules/{scheduleId:guid}/members")]
    [EnableRateLimiting("user-lookup")]
    public async Task<ActionResult<ScheduleMemberDto>> Invite(Guid scheduleId, InviteScheduleMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await memberService.InviteAsync(scheduleId, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Error!);
        }

        return Created($"/api/schedules/{scheduleId}/members/{result.Value!.UserId}", result.Value);
    }

    [HttpPut("api/schedules/{scheduleId:guid}/members/{userId:guid}")]
    public async Task<ActionResult<ScheduleMemberDto>> ChangeRole(
        Guid scheduleId,
        Guid userId,
        ChangeScheduleMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await memberService.ChangeRoleAsync(scheduleId, userId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result.Error!);
    }

    [HttpDelete("api/schedules/{scheduleId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> Remove(Guid scheduleId, Guid userId, CancellationToken cancellationToken)
    {
        var result = await memberService.RemoveAsync(scheduleId, userId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    [HttpGet("api/schedule-invitations")]
    public async Task<ActionResult<IReadOnlyList<ScheduleInvitationDto>>> GetInvitations(CancellationToken cancellationToken)
    {
        return Ok(await memberService.GetInvitationsAsync(cancellationToken));
    }

    [HttpPost("api/schedule-invitations/{scheduleId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid scheduleId, CancellationToken cancellationToken)
    {
        var result = await memberService.AcceptInvitationAsync(scheduleId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    [HttpPost("api/schedule-invitations/{scheduleId:guid}/decline")]
    public async Task<IActionResult> Decline(Guid scheduleId, CancellationToken cancellationToken)
    {
        var result = await memberService.DeclineInvitationAsync(scheduleId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result.Error!);
    }

    private ObjectResult ToErrorResponse(ScheduleError error)
    {
        var status = error.Type switch
        {
            ScheduleErrorType.NotFound => StatusCodes.Status404NotFound,
            ScheduleErrorType.Conflict => StatusCodes.Status409Conflict,
            ScheduleErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = new ProblemDetails { Status = status, Detail = error.Message };
        return new ObjectResult(problem) { StatusCode = status };
    }
}
