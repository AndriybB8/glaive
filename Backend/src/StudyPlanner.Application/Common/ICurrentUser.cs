namespace StudyPlanner.Application.Common;

/// <summary>
/// Dane zalogowanego użytkownika. Implementacja znajduje się w warstwie Api.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
}
