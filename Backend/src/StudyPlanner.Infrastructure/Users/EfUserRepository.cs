using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPlanner.Application.Users;
using StudyPlanner.Domain.Users;
using StudyPlanner.Infrastructure.Persistence;

namespace StudyPlanner.Infrastructure.Users;

public class EfUserRepository(AppDbContext db) : IUserRepository
{
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return db.Users.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            throw new UserEmailTakenException();
        }
    }
}
