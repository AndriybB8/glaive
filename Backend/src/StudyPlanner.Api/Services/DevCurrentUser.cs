using StudyPlanner.Application.Common;

namespace StudyPlanner.Api.Services;

/// <summary>
/// TYMCZASOWE: dopóki nie ma logowania (JWT), wszystkie notatki należą do jednego testowego użytkownika.
/// Po wdrożeniu uwierzytelniania zastąpić implementacją czytającą ID z claimów (HttpContext.User).
/// </summary>
public sealed class DevCurrentUser : ICurrentUser
{
    public static readonly Guid DevUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid UserId => DevUserId;
}
