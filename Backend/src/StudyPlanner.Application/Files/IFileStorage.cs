namespace StudyPlanner.Application.Files;

public interface IFileStorage
{
    Task SaveAsync(string relativePath, Stream stream, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken);
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken);
}
