using System.Security.Cryptography;
using System.Text;
using StudyPlanner.Application.Common;
using StudyPlanner.Application.Files;
using StudyPlanner.Domain.Files;

namespace StudyPlanner.UnitTests.Files;

public class FileServiceTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
    private const string Pdf = "application/pdf";

    private readonly FakeUploadedFileRepository _files = new();
    private readonly FakeFileStorage _storage = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(Now));
    private readonly TestUser _currentUser = new(OwnerId);
    private readonly FileService _service;

    public FileServiceTests()
    {
        _service = new FileService(_files, _storage, _currentUser, _time);
    }

    private static Stream Content(string text = "treść wykładu") => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private Task<FileResult<FileDto>> UploadAsync(string name = "wyklad.pdf", string contentType = Pdf, string text = "treść wykładu", long? size = null) =>
        _service.UploadAsync(name, contentType, Content(text), size ?? Encoding.UTF8.GetByteCount(text), CancellationToken.None);

    [Fact]
    public async Task Upload_ValidFile_CreatesRecordAndSavesToDisk()
    {
        var result = await UploadAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("wyklad.pdf", result.Value!.OriginalFileName);
        Assert.Equal(Pdf, result.Value.ContentType);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal(Now, result.Value.CreatedAt);
        Assert.Single(_storage.Saved);
        Assert.Single(_files.Files);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Upload_RequiresName(string? name)
    {
        var result = await UploadAsync(name!);

        Assert.Equal(FileErrorType.Validation, result.Error!.Type);
        Assert.Empty(_files.Files);
        Assert.Empty(_storage.Saved);
    }

    [Fact]
    public async Task Upload_RejectsTooLongName()
    {
        var name = new string('a', UploadedFile.MaxFileNameLength + 1) + ".pdf";

        var result = await UploadAsync(name);

        Assert.Equal(FileErrorType.Validation, result.Error!.Type);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("application/zip")]
    [InlineData("")]
    public async Task Upload_RejectsDisallowedContentType(string contentType)
    {
        var result = await UploadAsync(contentType: contentType);

        Assert.Equal(FileErrorType.Validation, result.Error!.Type);
        Assert.Empty(_files.Files);
        Assert.Empty(_storage.Saved);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("text/plain")]
    public async Task Upload_AllowsEveryWhitelistedType(string contentType)
    {
        var result = await UploadAsync(contentType: contentType);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Upload_RejectsFileOverSizeLimit()
    {
        var result = await UploadAsync(size: UploadedFile.MaxSizeBytes + 1);

        Assert.Equal(FileErrorType.Validation, result.Error!.Type);
        Assert.Empty(_files.Files);
        Assert.Empty(_storage.Saved);
    }

    [Fact]
    public async Task Upload_RejectsZeroSize()
    {
        var result = await UploadAsync(size: 0);

        Assert.Equal(FileErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Upload_IdenticalContentTwice_ReturnsExistingWithoutSavingAgain()
    {
        var first = await UploadAsync("wyklad1.pdf", text: "ta sama treść");

        var second = await UploadAsync("wyklad2.pdf", text: "ta sama treść");

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Equal("wyklad1.pdf", second.Value.OriginalFileName);
        Assert.Single(_storage.Saved);
        Assert.Single(_files.Files);
    }

    [Fact]
    public async Task Upload_DifferentContent_CreatesSeparateFiles()
    {
        await UploadAsync("a.pdf", text: "treść A");
        await UploadAsync("b.pdf", text: "treść B");

        Assert.Equal(2, _files.Files.Count);
        Assert.Equal(2, _storage.Saved.Count);
    }

    [Fact]
    public async Task Upload_SameContentDifferentOwners_CreatesSeparateFiles()
    {
        await UploadAsync(text: "wspólna treść");
        _currentUser.UserId = OtherUserId;

        var result = await UploadAsync(text: "wspólna treść");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _files.Files.Count);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyOwnFilesNewestFirst()
    {
        await UploadAsync("stary.pdf", text: "A");
        _time.Advance(TimeSpan.FromMinutes(1));
        await UploadAsync("nowy.pdf", text: "B");
        _currentUser.UserId = OtherUserId;
        await UploadAsync("cudzy.pdf", text: "C");
        _currentUser.UserId = OwnerId;

        var result = await _service.GetAllAsync(CancellationToken.None);

        Assert.Equal(["nowy.pdf", "stary.pdf"], result.Select(f => f.OriginalFileName));
    }

    [Fact]
    public async Task GetById_OfAnotherUser_IsNotFound()
    {
        var created = (await UploadAsync()).Value!;
        _currentUser.UserId = OtherUserId;

        var result = await _service.GetByIdAsync(created.Id, CancellationToken.None);

        Assert.Equal(FileErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Download_ReturnsContentFromStorage()
    {
        var created = (await UploadAsync(text: "zawartość do pobrania")).Value!;

        var result = await _service.DownloadAsync(created.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Pdf, result.Value!.ContentType);
        Assert.Equal("wyklad.pdf", result.Value.FileName);
        using var reader = new StreamReader(result.Value.Content);
        Assert.Equal("zawartość do pobrania", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Download_OfAnotherUser_IsNotFound()
    {
        var created = (await UploadAsync()).Value!;
        _currentUser.UserId = OtherUserId;

        var result = await _service.DownloadAsync(created.Id, CancellationToken.None);

        Assert.Equal(FileErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task ReplaceContent_WithCurrentVersion_SavesAndIncrementsVersion()
    {
        var created = (await UploadAsync(text: "stara treść")).Value!;
        var savesAfterUpload = _storage.SaveCount;
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.ReplaceContentAsync(created.Id, "wyklad.pdf", Pdf, Content("nowa treść"), 11, created.Version, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Version);
        Assert.True(result.Value.UpdatedAt > created.UpdatedAt);
        Assert.Equal(savesAfterUpload + 1, _storage.SaveCount);
    }

    [Fact]
    public async Task ReplaceContent_WithIdenticalContent_DoesNotIncrementVersionOrTouchDiskAgain()
    {
        var created = (await UploadAsync(text: "ta sama treść")).Value!;
        var savesAfterUpload = _storage.SaveCount;

        var result = await _service.ReplaceContentAsync(created.Id, "wyklad.pdf", Pdf, Content("ta sama treść"), 13, created.Version, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Version);
        Assert.Equal(savesAfterUpload, _storage.SaveCount);
    }

    [Fact]
    public async Task ReplaceContent_WithStaleVersion_ReturnsConflictWithCurrentFile()
    {
        var created = (await UploadAsync(text: "v1")).Value!;
        await _service.ReplaceContentAsync(created.Id, "wyklad.pdf", Pdf, Content("v2"), 2, created.Version, CancellationToken.None);

        var result = await _service.ReplaceContentAsync(created.Id, "wyklad.pdf", Pdf, Content("v3 z nieaktualnej karty"), 2, created.Version, CancellationToken.None);

        Assert.Equal(FileErrorType.Conflict, result.Error!.Type);
        Assert.Equal(2, result.Error.Current!.Version);
    }

    [Fact]
    public async Task ReplaceContent_RejectsDisallowedType()
    {
        var created = (await UploadAsync()).Value!;

        var result = await _service.ReplaceContentAsync(created.Id, "wyklad.pdf", "image/png", Content("x"), 1, created.Version, CancellationToken.None);

        Assert.Equal(FileErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task ReplaceContent_OfAnotherUser_IsNotFound()
    {
        var created = (await UploadAsync()).Value!;
        _currentUser.UserId = OtherUserId;

        var result = await _service.ReplaceContentAsync(created.Id, "wyklad.pdf", Pdf, Content("x"), 1, created.Version, CancellationToken.None);

        Assert.Equal(FileErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Delete_RemovesFileAndDeletesFromDisk()
    {
        var created = (await UploadAsync()).Value!;

        var result = await _service.DeleteAsync(created.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_files.Files);
        Assert.Single(_storage.Deleted);
    }

    [Fact]
    public async Task Delete_NotFound_DoesNotTouchStorage()
    {
        var result = await _service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(FileErrorType.NotFound, result.Error!.Type);
        Assert.Empty(_storage.Deleted);
    }

    [Fact]
    public async Task Delete_OfAnotherUser_IsNotFound()
    {
        var created = (await UploadAsync()).Value!;
        _currentUser.UserId = OtherUserId;

        var result = await _service.DeleteAsync(created.Id, CancellationToken.None);

        Assert.Equal(FileErrorType.NotFound, result.Error!.Type);
        Assert.Single(_files.Files);
    }

    [Fact]
    public async Task ReplaceContent_RenameWithIdenticalContent_UpdatesNameAndVersionWithoutTouchingDisk()
    {
        var created = (await UploadAsync("stara-nazwa.pdf", text: "ta sama treść")).Value!;
        var savesAfterUpload = _storage.SaveCount;

        var result = await _service.ReplaceContentAsync(created.Id, "nowa-nazwa.pdf", Pdf, Content("ta sama treść"), 13, created.Version, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("nowa-nazwa.pdf", result.Value!.OriginalFileName);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal(savesAfterUpload, _storage.SaveCount);
    }

    private sealed class TestUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; set; } = userId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private sealed class FakeUploadedFileRepository : IUploadedFileRepository
    {
        public List<UploadedFile> Files { get; } = [];

        public Task<IReadOnlyList<UploadedFile>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UploadedFile>>(Files.Where(f => f.OwnerId == ownerId).ToList());

        public Task<UploadedFile?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken cancellationToken) =>
            Task.FromResult(Files.FirstOrDefault(f => f.Id == id && f.OwnerId == ownerId));

        public Task<UploadedFile?> GetByOwnerAndHashAsync(Guid ownerId, string contentHash, CancellationToken cancellationToken) =>
            Task.FromResult(Files.FirstOrDefault(f => f.OwnerId == ownerId && f.ContentHash == contentHash));

        public Task AddAsync(UploadedFile file, CancellationToken cancellationToken)
        {
            Files.Add(file);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UploadedFile file, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(UploadedFile file, CancellationToken cancellationToken)
        {
            Files.Remove(file);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFileStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Saved { get; } = [];

        public List<string> Deleted { get; } = [];

        public int SaveCount { get; private set; }

        public async Task SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Saved[relativePath] = buffer.ToArray();
            SaveCount++;
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(Saved[relativePath]));

        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken)
        {
            Saved.Remove(relativePath);
            Deleted.Add(relativePath);
            return Task.CompletedTask;
        }
    }
}
