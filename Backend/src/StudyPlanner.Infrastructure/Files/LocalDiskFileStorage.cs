using Microsoft.Extensions.Configuration;
using StudyPlanner.Application.Files;

namespace StudyPlanner.Infrastructure.Files;

public class LocalDiskFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalDiskFileStorage(IConfiguration config)
    {
        var configRoot = config["FileStorage:RootPath"] ?? ".uploaded-files";
        _root = Path.GetFullPath(configRoot, Directory.GetCurrentDirectory());
    }

    public async Task SaveAsync(string relativePath, Stream stream, CancellationToken cancellationToken)
    {
        var fullPath = ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fstream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fstream, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        var fullPath = ResolvePath(relativePath);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken)
    {
        var fullPath = ResolvePath(relativePath);
        File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string ResolvePath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));

        if (!fullPath.StartsWith(_root, StringComparison.Ordinal))
            throw new InvalidOperationException($"Ścieżka '{relativePath}' wychodzi poza katalog przechowywania plików.");

        return fullPath;
    }
}
