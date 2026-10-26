using System.Text.RegularExpressions;

namespace StudyPlanner.Domain.Users;

public enum AccountStatus
{
    Active = 0,
    Disabled
}

public class User
{
    public const int MaxEmailLength = 255;

    public Guid Id { get; private set; }
    public string Email { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";

    public DateTime CreatedAt { get; private set; }
    public AccountStatus Status { get; private set; }

    private User() {}

    public static User Create(string email, string passwordHash, DateTime utcNow)
    {
        EnsureValid(email, passwordHash);
        EnsureUtc(utcNow);

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            CreatedAt = utcNow,
            Status = AccountStatus.Active
        };
    }

    private static void EnsureUtc(DateTime time)
    {
        if (time.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC time. ", nameof(time));
        }
    }

    private static void EnsureValid(string email, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        if (email.Length > MaxEmailLength)
        {
            throw new ArgumentException("The email is too long.", nameof(email));
        }

        if (!IsValidEmail(email))
        {
            throw new ArgumentException("The email is invalid.", nameof(email));
        }
    }

    private static bool IsValidEmail(string email)
    {
        //xxx@yyy
        var at = email.IndexOf('@');
        //yyy
        var dom = email.Substring(at + 1);


        return  (at > 0 && at < email.Length - 1)             && //@yyy or xxx@
                (email.IndexOf('@', at + 1) == -1)   && //xxx@yyy@zzz
                (email.All(c => !char.IsWhiteSpace(c)))  && //xxx @yyy
                dom.Contains('.')                             && //xxx@yyy.zzz
                !dom.StartsWith('.')                          && //xxx@yyy..zzz
                !dom.EndsWith('.');                              //xxx@yyy..
    }
}
