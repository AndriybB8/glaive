using StudyPlanner.Application.Common;
using StudyPlanner.Application.Users;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.UnitTests.Users;

public class UserServiceTests
{
    private readonly FakeUserRepository _repo = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly UserService _service;

    public UserServiceTests()
    {
        _service = new UserService(_repo, _hasher, _time);
    }

    [Fact]
    public async Task Register_WithValidData_CreatesActiveUserWithHashedPassword()
    {
        var request = new RegisterRequest("test@mail.com", "haslo123");

        var result = await _service.RegisterAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("test@mail.com", result.Value!.Email);
        Assert.Equal(AccountStatus.Active, result.Value.Status);

        var stored = Assert.Single(_repo.Users);
        Assert.NotEqual("haslo123", stored.PasswordHash);
        Assert.Equal(_hasher.Hash("haslo123"), stored.PasswordHash);
    }

    [Fact]
    public async Task Register_NormalizesEmailCasing()
    {
        var result = await _service.RegisterAsync(new RegisterRequest("Test@Mail.com", "haslo123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("test@mail.com", result.Value!.Email);
    }

    [Fact]
    public async Task Register_WithTakenEmail_ReturnsValidationError()
    {
        await _service.RegisterAsync(new RegisterRequest("test@mail.com", "haslo123"), CancellationToken.None);

        var result = await _service.RegisterAsync(new RegisterRequest("TEST@MAIL.COM", "inneHaslo1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UserErrorType.Validation, result.Error!.Type);
        Assert.Single(_repo.Users);
    }

    [Theory]
    [InlineData("")]
    [InlineData("brak-malpy.com")]
    [InlineData("user@baddomain")]
    [InlineData("@brakloginu.com")]
    public async Task Register_WithInvalidEmail_ReturnsValidationError(string email)
    {
        var result = await _service.RegisterAsync(new RegisterRequest(email, "haslo123"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UserErrorType.Validation, result.Error!.Type);
        Assert.Empty(_repo.Users);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short1")]
    public async Task Register_WithTooShortPassword_ReturnsValidationError(string password)
    {
        var result = await _service.RegisterAsync(new RegisterRequest("test@mail.com", password), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UserErrorType.Validation, result.Error!.Type);
        Assert.Empty(_repo.Users);
    }

    [Fact]
    public async Task Register_WhenRepositoryDetectsRaceOnEmail_ReturnsValidationError()
    {
        _repo.ThrowEmailTakenOnNextAdd = true;

        var result = await _service.RegisterAsync(new RegisterRequest("test@mail.com", "haslo123"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UserErrorType.Validation, result.Error!.Type);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string hash, string password) => hash == Hash(password);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];
        public bool ThrowEmailTakenOnNextAdd { get; set; }

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Users.Any(u => u.Email == email));

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            if (ThrowEmailTakenOnNextAdd)
            {
                ThrowEmailTakenOnNextAdd = false;
                throw new UserEmailTakenException();
            }

            Users.Add(user);
            return Task.CompletedTask;
        }
    }
}

