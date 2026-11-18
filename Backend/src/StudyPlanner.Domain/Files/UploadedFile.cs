namespace StudyPlanner.Domain.Files;

public class UploadedFile
{
    public const int MaxFileNameLength = 255;
    public const long MaxSizeBytes = 5 * 1024 * 1024; // 5MB, change this to increase/decrease max size per file

    public static readonly IReadOnlyCollection<string> AllowedContentTypes =
    [
        "application/pdf",
        "application/msword", //doc
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", //docx lol
        "text/plain", //txt
    ];

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }

    public string FileName { get; private set; } = string.Empty;
    public string StoragePath { get;private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public string ContentHash { get; private set; } = string.Empty;
    public int Version { get; private set; }

    private UploadedFile() {}

    public static UploadedFile Create(
        Guid ownerId,
        string fileName,
        string storagePath,
        string contentType,
        long sizeBytes,
        string contentHash,
        DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(fileName, storagePath, contentType, sizeBytes, contentHash);

        return new UploadedFile
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            FileName = fileName,
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            ContentHash = contentHash,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Version = 1,
        };
    }

    public void ReplaceContent(string fileName, string storagePath, string contentType, long sizeBytes, string contentHash, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(FileName, storagePath, contentType, sizeBytes, contentHash);

        FileName = fileName;
        StoragePath = storagePath;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        ContentHash = contentHash;
        UpdatedAt = utcNow;
        Version++;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Data musi być w UTC.", nameof(value));
        }
    }

    private static void EnsureValid(string originalFileName, string storagePath, string contentType, long sizeBytes, string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        if (originalFileName.Length > MaxFileNameLength)
        {
            throw new ArgumentException($"Nazwa pliku może mieć maksymalnie {MaxFileNameLength} znaków.", nameof(originalFileName));
        }

        if (sizeBytes <= 0 || sizeBytes > MaxSizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), $"Rozmiar pliku musi mieścić się w zakresie 1–{MaxSizeBytes} bajtów.");
        }

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new ArgumentException($"Niedozwolony typ pliku: {contentType}.", nameof(contentType));
        }

        if (contentHash.Length != 64)
        {
            throw new ArgumentException("Hash SHA-256 musi mieć 64 znaki (hex).", nameof(contentHash));
        }
    }
}
