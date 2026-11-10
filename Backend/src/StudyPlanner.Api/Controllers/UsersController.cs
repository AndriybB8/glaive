using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyPlanner.Application.Users;

namespace StudyPlanner.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(UserService userService) : ControllerBase
{
    /// <summary>
    /// Wyszukanie aktywnego konta po dokładnym adresie email (okno udostępniania listy).
    /// POST, bo adres w query stringu trafiałby do logów. Limit zapytań, bo odpowiedź zdradza, czy adres ma konto.
    /// </summary>
    [HttpPost("lookup")]
    [EnableRateLimiting("user-lookup")]
    public async Task<ActionResult<UserLookupDto>> Lookup(UserLookupRequest request, CancellationToken cancellationToken)
    {
        var result = await userService.LookupAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var error = result.Error!;
        var status = error.Type == UserErrorType.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;
        var problem = new ProblemDetails { Status = status, Detail = error.Message };

        return new ObjectResult(problem) { StatusCode = status };
    }
}
