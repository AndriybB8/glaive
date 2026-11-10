using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.SignalR;

namespace StudyPlanner.Api.Hubs;

/// <summary>
/// SignalR domyślnie szuka użytkownika w ClaimTypes.NameIdentifier, a przy MapInboundClaims = false
/// identyfikator zostaje w „sub”. Bez tej klasy wysyłka do użytkowników po cichu nie trafiałaby do nikogo.
/// </summary>
public sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
