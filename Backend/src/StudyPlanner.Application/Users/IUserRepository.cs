using StudyPlanner.Domain.Users;

namespace StudyPlanner.Application.Users;

public interface IUserRepository
{
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
}

public class UserEmailTakenException() : Exception("This email is already registered.");
