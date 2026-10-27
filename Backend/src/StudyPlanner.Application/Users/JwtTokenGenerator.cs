using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StudyPlanner.Application.Common;

namespace StudyPlanner.Application.Users;

public class JwtTokenGenerator(IConfiguration config, TimeProvider time) : ITokenGenerator
{
    //Temporary, parametrize later?
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public GeneratedToken GenerateToken(Guid userId, string email)
    {
        if (config["Jwt:Key"] == null) throw new InvalidOperationException("Brak Jwt:Key w konfiguracji.");
        var key = Encoding.UTF8.GetBytes(config["Jwt:Key"]!);

        var symmKey = new SymmetricSecurityKey(key);
        var creds = new SigningCredentials(symmKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
        };

        var descr = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = time.GetUtcNow().Add(TokenLifetime).UtcDateTime,
            SigningCredentials = creds,
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descr);
        return new GeneratedToken(handler.WriteToken(token), token.ValidTo);
    }
}
