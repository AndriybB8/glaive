using StudyPlanner.Domain.Tags;

namespace StudyPlanner.Application.Tags;

public sealed record TagDto(Guid Id, string Name, string Color, DateTime CreatedAt, DateTime UpdatedAt, int Version)
{
    public static TagDto From(Tag tag) =>
        new(tag.Id, tag.Name, tag.Color, tag.CreatedAt, tag.UpdatedAt, tag.Version);
}

public sealed record CreateTagRequest(string? Name, string? Color);

public sealed record UpdateTagRequest(string? Name, string? Color, int Version);
