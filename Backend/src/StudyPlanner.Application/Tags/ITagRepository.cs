using StudyPlanner.Domain.Tags;

namespace StudyPlanner.Application.Tags;

public interface ITagRepository
{
    Task<IReadOnlyList<Tag>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Tag?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    Task AddAsync(Tag tag, CancellationToken cancellationToken);

    Task UpdateAsync(Tag tag, CancellationToken cancellationToken);

    Task DeleteAsync(Tag tag, CancellationToken cancellationToken);
}

public sealed class TagNameTakenException() : Exception("Tag o tej nazwie już istnieje.");

public sealed class TagConcurrencyException() : Exception("Tag został w międzyczasie zmieniony w innym miejscu.");
