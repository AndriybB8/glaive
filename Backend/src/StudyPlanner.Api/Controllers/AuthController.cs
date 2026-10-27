using Microsoft.AspNetCore.Authorization;
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
        return res.IsSuccess ? Ok(res.Value) : ToErrorResponse(res.Error!);
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequest req, CancellationToken cancellationToken)
    {
        var res = await userService.LoginAsync(req, cancellationToken);
        return res.IsSuccess ? Ok(res.Value) : ToErrorResponse(res.Error!);
    }

    private static ObjectResult ToErrorResponse(UserError error)
    {
        var problem = new ProblemDetails { Status = StatusCodes.Status400BadRequest, Detail = error.Message };
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
    }
}
