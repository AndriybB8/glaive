using System.Text.RegularExpressions;

namespace StudyPlanner.Domain.Tags;

public class Tag
{
    private static readonly Regex HexColor = new Regex(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
    public const int NameMaxLength = 50;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public int Version { get; private set; }

    private Tag(){}

    public static Tag Create(Guid userId, string name, string color, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(name, color);

        return new Tag
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Color = color,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Version = 1,
        };
    }

    public void Update(string name, string color, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(name, color);

        Name = name;
        Color = color;
        UpdatedAt = utcNow;
        Version++;
    }

    public static bool IsValidColor(string color) => HexColor.IsMatch(color);

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Data musi być w UTC.", nameof(value));
        }
    }

    private static void EnsureValid(string name, string color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(color);

        if (name.Length > NameMaxLength)
        {
            throw new ArgumentException($"Nazwa tagu może mieć maksymalnie {NameMaxLength} znaków.", nameof(name));
        }

        if (!IsValidColor(color))
        {
            throw new ArgumentException("Kolor musi być w formacie szesnastkowym, np. #3B82F6.", nameof(color));
        }
    }
}

