using StudyPlanner.Application.Common;
using StudyPlanner.Application.Pomodoro;
using StudyPlanner.Domain.Pomodoro;

namespace StudyPlanner.UnitTests.Pomodoro;

public class PomodoroSettingsServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly FakePomodoroSettingsRepository _repository = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly PomodoroSettingsService _service;

    public PomodoroSettingsServiceTests()
    {
        _service = new PomodoroSettingsService(_repository, new TestUser(UserId), _time);
    }

    [Fact]
    public async Task Get_WithoutSavedSettings_ReturnsDefaultsWithoutSaving()
    {
        var settings = await _service.GetAsync(CancellationToken.None);

        Assert.Equal(new PomodoroSettingsDto(25, 5, 15, 4, IsDefault: true), settings);
        Assert.Empty(_repository.Settings);
    }

    [Fact]
    public async Task Get_ReturnsSavedSettings()
    {
        _repository.Settings.Add(PomodoroSettings.Create(UserId, 50, 10, 30, 3, DateTime.UtcNow));

        var settings = await _service.GetAsync(CancellationToken.None);

        Assert.Equal(new PomodoroSettingsDto(50, 10, 30, 3, IsDefault: false), settings);
    }

    [Fact]
    public async Task Get_IgnoresSettingsOfOtherUsers()
    {
        _repository.Settings.Add(PomodoroSettings.Create(Guid.NewGuid(), 50, 10, 30, 3, DateTime.UtcNow));

        var settings = await _service.GetAsync(CancellationToken.None);

        Assert.True(settings.IsDefault);
    }

    [Fact]
    public async Task Update_FirstTime_CreatesSettingsForCurrentUser()
    {
        var result = await _service.UpdateAsync(new UpdatePomodoroSettingsRequest(50, 10, 30, 3), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new PomodoroSettingsDto(50, 10, 30, 3, IsDefault: false), result.Value);

        var saved = Assert.Single(_repository.Settings);
        Assert.Equal(UserId, saved.UserId);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
        Assert.Equal(saved.CreatedAt, saved.UpdatedAt);
    }

    [Fact]
    public async Task Update_ExistingSettings_OverwritesValuesAndUpdatedAt()
    {
        await _service.UpdateAsync(new UpdatePomodoroSettingsRequest(50, 10, 30, 3), CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.UpdateAsync(new UpdatePomodoroSettingsRequest(45, 5, 20, 2), CancellationToken.None);

        Assert.Equal(new PomodoroSettingsDto(45, 5, 20, 2, IsDefault: false), result.Value);

        var saved = Assert.Single(_repository.Settings);
        Assert.Equal(45, saved.WorkMinutes);
        Assert.True(saved.UpdatedAt > saved.CreatedAt);
        Assert.Equal(1, _repository.UpdateCalls);
    }

    [Fact]
    public async Task Update_WithoutChanges_DoesNotSaveOrTouchUpdatedAt()
    {
        await _service.UpdateAsync(new UpdatePomodoroSettingsRequest(50, 10, 30, 3), CancellationToken.None);
        var updatedAt = _repository.Settings.Single().UpdatedAt;
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.UpdateAsync(new UpdatePomodoroSettingsRequest(50, 10, 30, 3), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(updatedAt, _repository.Settings.Single().UpdatedAt);
        Assert.Equal(0, _repository.UpdateCalls);
    }

    [Fact]
    public async Task Update_WhenOtherTabCreatedSettingsFirst_OverwritesThem()
    {
        // Druga karta zapisała ustawienia między odczytem a dodaniem – baza odrzuca drugi wiersz.
        _repository.BeforeAdd = () => _repository.Settings.Add(PomodoroSettings.Create(UserId, 60, 10, 30, 4, DateTime.UtcNow));

        var result = await _service.UpdateAsync(new UpdatePomodoroSettingsRequest(50, 10, 30, 3), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = Assert.Single(_repository.Settings);
        Assert.Equal(50, saved.WorkMinutes);
        Assert.Equal(3, saved.SessionsBeforeLongBreak);
    }

    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(120, 30, 60, 10)]
    public async Task Update_AcceptsBoundaryValues(int work, int shortBreak, int longBreak, int sessions)
    {
        var result = await _service.UpdateAsync(new UpdatePomodoroSettingsRequest(work, shortBreak, longBreak, sessions), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(null, 5, 15, 4, "Podaj czas pracy.")]
    [InlineData(0, 5, 15, 4, "Czas pracy musi wynosić od 1 do 120 minut.")]
    [InlineData(121, 5, 15, 4, "Czas pracy musi wynosić od 1 do 120 minut.")]
    [InlineData(25, null, 15, 4, "Podaj czas krótkiej przerwy.")]
    [InlineData(25, 0, 15, 4, "Krótka przerwa musi wynosić od 1 do 30 minut.")]
    [InlineData(25, 31, 15, 4, "Krótka przerwa musi wynosić od 1 do 30 minut.")]
    [InlineData(25, 5, null, 4, "Podaj czas długiej przerwy.")]
    [InlineData(25, 5, 0, 4, "Długa przerwa musi wynosić od 1 do 60 minut.")]
    [InlineData(25, 5, 61, 4, "Długa przerwa musi wynosić od 1 do 60 minut.")]
    [InlineData(25, 5, 15, null, "Podaj liczbę sesji przed długą przerwą.")]
    [InlineData(25, 5, 15, 0, "Liczba sesji przed długą przerwą musi wynosić od 1 do 10.")]
    [InlineData(25, 5, 15, 11, "Liczba sesji przed długą przerwą musi wynosić od 1 do 10.")]
    public async Task Update_RejectsMissingOrOutOfRangeValues(int? work, int? shortBreak, int? longBreak, int? sessions, string message)
    {
        var result = await _service.UpdateAsync(new UpdatePomodoroSettingsRequest(work, shortBreak, longBreak, sessions), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PomodoroSettingsErrorType.Validation, result.Error!.Type);
        Assert.Equal(message, result.Error.Message);
        Assert.Empty(_repository.Settings);
    }

    [Fact]
    public async Task Update_DoesNotChangeSettingsOfOtherUsers()
    {
        var foreign = PomodoroSettings.Create(Guid.NewGuid(), 60, 10, 30, 4, DateTime.UtcNow);
        _repository.Settings.Add(foreign);

        await _service.UpdateAsync(new UpdatePomodoroSettingsRequest(50, 10, 30, 3), CancellationToken.None);

        Assert.Equal(2, _repository.Settings.Count);
        Assert.Equal(60, foreign.WorkMinutes);
    }

    private sealed class TestUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private sealed class FakePomodoroSettingsRepository : IPomodoroSettingsRepository
    {
        public List<PomodoroSettings> Settings { get; } = [];

        public int UpdateCalls { get; private set; }

        /// <summary>Wywoływane tuż przed dodaniem – do symulowania zapisu z innej karty.</summary>
        public Action? BeforeAdd { get; set; }

        public Task<PomodoroSettings?> GetByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Settings.FirstOrDefault(s => s.UserId == userId));

        public Task AddAsync(PomodoroSettings settings, CancellationToken cancellationToken)
        {
            BeforeAdd?.Invoke();

            // Jak klucz główny user_id w bazie.
            if (Settings.Any(s => s.UserId == settings.UserId))
            {
                throw new PomodoroSettingsAlreadyExistsException();
            }

            Settings.Add(settings);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PomodoroSettings settings, CancellationToken cancellationToken)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }
    }
}
