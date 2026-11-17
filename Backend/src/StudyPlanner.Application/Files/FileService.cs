using System.Security.Cryptography;
using StudyPlanner.Application.Common;
using StudyPlanner.Domain.Files;

namespace StudyPlanner.Application.Files;

public sealed class FileService(
    IUploadedFileRepository files,
    IFileStorage storage,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly FileError NotFound = new(FileErrorType.NotFound, "Nie znaleziono pliku.");

    public async Task<IReadOnlyList<FileDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var found = await files.GetByOwnerAsync(currentUser.UserId, cancellationToken);
        return found.OrderByDescending(f => f.CreatedAt).Select(FileDto.From).ToList();
    }


    public async Task<FileResult<FileDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var file = await files.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        return file is null ? FileResult<FileDto>.Failure(NotFound) : FileResult<FileDto>.Success(FileDto.From(file));
    }


    public async Task<FileResult<FileContent>> DownloadAsync(Guid id, CancellationToken cancellationToken)
    {
        var file = await files.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (file is null)
            return FileResult<FileContent>.Failure(NotFound);

        var stream = await storage.OpenReadAsync(file.StoragePath, cancellationToken);
        return FileResult<FileContent>.Success(new FileContent(stream, file.ContentType, file.FileName));
    }


    public async Task<FileResult<FileDto>> UploadAsync(string originalFileName, string contentType, Stream content, long sizeBytes, CancellationToken cancellationToken)
    {
        var name = originalFileName?.Trim() ?? string.Empty;
        if (Validate(name, contentType, sizeBytes) is { } error)
            return FileResult<FileDto>.Failure(error);

        var (bytes, hash) = await ReadAndHashAsync(content, cancellationToken);

        var existing = await files.GetByOwnerAndHashAsync(currentUser.UserId, hash, cancellationToken);
        if (existing is not null)
            return FileResult<FileDto>.Success(FileDto.From(existing));

        var id = Guid.NewGuid();
        var storagePath = $"{currentUser.UserId}/{id}{Path.GetExtension(name)}";

        await storage.SaveAsync(storagePath, new MemoryStream(bytes), cancellationToken);

        var file = UploadedFile.Create(currentUser.UserId, name, storagePath, contentType, sizeBytes, hash, UtcNow());
        await files.AddAsync(file, cancellationToken);

        return FileResult<FileDto>.Success(FileDto.From(file));
    }


    public async Task<FileResult<FileDto>> ReplaceContentAsync(
        Guid id,
        string originalFileName,
        string contentType,
        Stream content,
        long sizeBytes,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var name = originalFileName?.Trim() ?? string.Empty;
        if (Validate(name, contentType, sizeBytes) is { } error)
            return FileResult<FileDto>.Failure(error);

        var file = await files.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (file is null)
            return FileResult<FileDto>.Failure(NotFound);

        if (file.Version != expectedVersion)
            return FileResult<FileDto>.Failure(Conflict(file));

        var (bytes, hash) = await ReadAndHashAsync(content, cancellationToken);

        if (file.ContentHash == hash && file.FileName == name)
            return FileResult<FileDto>.Success(FileDto.From(file));

        if (file.ContentHash != hash)
            await storage.SaveAsync(file.StoragePath, new MemoryStream(bytes), cancellationToken);

        file.ReplaceContent(name, file.StoragePath, contentType, sizeBytes, hash, UtcNow());

        try
        {
            await files.UpdateAsync(file, cancellationToken);
        }
        catch (FileConcurrencyException)
        {
            var current = await files.GetByIdAsync(id, currentUser.UserId, cancellationToken);
            return current is null
                ? FileResult<FileDto>.Failure(NotFound)
                : FileResult<FileDto>.Failure(Conflict(current));
        }

        return FileResult<FileDto>.Success(FileDto.From(file));
    }


    public async Task<FileResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var file = await files.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (file is null)
            return FileResult<bool>.Failure(NotFound);

        await storage.DeleteAsync(file.StoragePath, cancellationToken);
        await files.DeleteAsync(file, cancellationToken);

        return FileResult<bool>.Success(true);
    }


    private static async Task<(byte[] Bytes, string Hash)> ReadAndHashAsync(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        return (bytes, hash);
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static FileError Conflict(UploadedFile current) =>
        new(FileErrorType.Conflict, "Plik został w międzyczasie zmieniony w innym miejscu.", FileDto.From(current));

    private static FileError? Validate(string name, string contentType, long sizeBytes)
    {
        if (name.Length == 0)
            return Invalid("Podaj nazwę pliku.");

        if (name.Length > UploadedFile.MaxFileNameLength)
            return Invalid($"Nazwa pliku może mieć maksymalnie {UploadedFile.MaxFileNameLength} znaków.");

        if (!UploadedFile.AllowedContentTypes.Contains(contentType))
        {
            return Invalid("Dozwolone typy plików to PDF, DOC, DOCX i TXT.");
        }

        if (sizeBytes <= 0 || sizeBytes > UploadedFile.MaxSizeBytes)
            return Invalid($"Plik może mieć maksymalnie {UploadedFile.MaxSizeBytes / (1024 * 1024)}MB.");

        return null;
    }

    private static FileError Invalid(string message) => new(FileErrorType.Validation, message);
}

