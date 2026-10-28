using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using StudyPlanner.Application.Common;

namespace StudyPlanner.Application.Users;

public class HttpContextCurrentUser(IHttpContextAccessor httpCtx) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var subject = httpCtx.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? throw new InvalidOperationException("Brak zalogowanego uzytkownika w kontekście requestu.");
            return Guid.Parse(subject);
        }
    }
}
