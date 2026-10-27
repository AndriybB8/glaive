using StudyPlanner.Application.Common;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Application.Users;

public class UserService(IUserRepository repo, IPasswordHasher hasher, TimeProvider timeProvider)
{
    private const int MinPasswordLength = 8;


    public async Task<UserResult<UserDto>> RegisterAsync(RegisterRequest req,
        CancellationToken cancellationToken)
    {
        var email = req.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var password = req.Password ?? string.Empty;

        if (email.Length == 0)
            return UserResult<UserDto>.Failure(new UserError(UserErrorType.Validation,
                "Podaj adres email."));

        if (email.Length > User.MaxEmailLength)
        {
            return UserResult<UserDto>.Failure(new UserError(UserErrorType.Validation, "Adres email jest za długi."));
        }

        if (!User.IsValidEmail(email))
        {
            return UserResult<UserDto>.Failure(new UserError(UserErrorType.Validation, "Adres email jest nieprawidłowy."));
        }

        if (password.Length < MinPasswordLength)
        {
            return UserResult<UserDto>.Failure(new UserError(UserErrorType.Validation,
                $"Hasło musi mieć co najmniej {MinPasswordLength} znaków."));
        }

        if (await repo.ExistsByEmailAsync(email, cancellationToken))
        {
            return UserResult<UserDto>.Failure(new UserError(UserErrorType.Validation, "Ten adres email jest już zajęty."));
        }

        var hash = hasher.Hash(password);
        var user = User.Create(email, hash, UtcNow());

        try
        {
            await repo.AddAsync(user, cancellationToken);
        }
        catch (UserEmailTakenException)
        {
            return UserResult<UserDto>.Failure(new UserError(UserErrorType.Validation, "This email is already registered."));
        }

        return UserResult<UserDto>.Success(UserDto.From(user));
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
