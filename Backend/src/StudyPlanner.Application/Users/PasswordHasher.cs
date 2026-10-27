using Microsoft.AspNetCore.Identity;
using StudyPlanner.Application.Common;

namespace StudyPlanner.Application.Users;

public class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password)
    {
        return _hasher.HashPassword(null! , password); //xD
    }

    public bool Verify(string hash, string password)
    {
        return _hasher.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;
    }
}
