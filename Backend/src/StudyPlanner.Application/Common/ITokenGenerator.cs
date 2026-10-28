namespace StudyPlanner.Application.Common;

public sealed record GeneratedToken(string Token, DateTime ExpiresAt);

public interface ITokenGenerator
{
    GeneratedToken GenerateToken(Guid userId, string email);
}
