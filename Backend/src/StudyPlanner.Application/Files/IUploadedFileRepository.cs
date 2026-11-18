using StudyPlanner.Domain.Files;

namespace StudyPlanner.Application.Files;

public interface IUploadedFileRepository
{
    Task<IReadOnlyList<UploadedFile>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken);

    Task<UploadedFile?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken cancellationToken);

    Task<UploadedFile?> GetByOwnerAndHashAsync(Guid ownerId, string contentHash, CancellationToken cancellationToken);

    Task AddAsync(UploadedFile file, CancellationToken cancellationToken);

    Task UpdateAsync(UploadedFile file, CancellationToken cancellationToken);

    Task DeleteAsync(UploadedFile file, CancellationToken cancellationToken);
}

public sealed class FileConcurrencyException() : Exception("Plik został zmieniony przez inny zapis.");
