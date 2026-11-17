using StudyPlanner.Domain.Files;

namespace StudyPlanner.Application.Files;

public record FileDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version)
{
    public static FileDto From(UploadedFile file) =>
        new(file.Id, file.FileName, file.ContentType, file.SizeBytes, file.CreatedAt, file.UpdatedAt, file.Version);
}

public sealed record FileContent(Stream Content, string ContentType, string FileName);
