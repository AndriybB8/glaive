using StudyPlanner.Domain.Users;

namespace StudyPlanner.Application.Users;


public sealed record RegisterRequest(string? Email, string? Password);

public sealed record UserDto(Guid Id, string Email, DateTime CreatedAt, AccountStatus Status)
{
    public static UserDto From(User user)
    {
        return new UserDto(user.Id, user.Email, user.CreatedAt, user.Status);
    }
}

