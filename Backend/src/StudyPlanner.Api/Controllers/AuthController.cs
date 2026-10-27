using Microsoft.AspNetCore.Mvc;
using StudyPlanner.Application.Users;

namespace StudyPlanner.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserService userService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest req, CancellationToken cancellationToken)
    {
        var res = await userService.RegisterAsync(req, cancellationToken);
        return res.IsSuccess ? Ok(res.Value) : BadRequest(res.Error);
    }

    private static ObjectResult ToErrorReponse(UserError error)
    {
        var problem = new ProblemDetails { Status = StatusCodes.Status400BadRequest, Detail = error.Message };
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
    }
}
