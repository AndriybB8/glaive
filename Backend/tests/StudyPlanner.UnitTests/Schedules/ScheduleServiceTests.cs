using StudyPlanner.Application.Common;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Domain.Schedules;

namespace StudyPlanner.UnitTests.Schedules;

public class ScheduleServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Jan25 = new(2027, 1, 25);
    private static readonly DateOnly Feb14 = new(2027, 2, 14);

    private readonly FakeScheduleRepository _repository = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
    private readonly ScheduleService _service;

    public ScheduleServiceTests()
    {
        _service = new ScheduleService(_repository, new TestUser(UserId), _time);
    }

    [Fact]
    public async Task Create_SetsOwnerDatesAndFirstVersion()
    {
        var result = await _service.CreateAsync(new CreateScheduleRequest("  Sesja zimowa  ", Jan25, Feb14), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var schedule = result.Value!;
        Assert.Equal("Sesja zimowa", schedule.Name);
        Assert.Equal(Jan25, schedule.StartDate);
        Assert.Equal(Feb14, schedule.EndDate);
        Assert.Equal(1, schedule.Version);
        Assert.Equal(DateTimeKind.Utc, schedule.CreatedAt.Kind);
        Assert.Equal(schedule.CreatedAt, schedule.UpdatedAt);
        Assert.Equal(UserId, _repository.Schedules.Single().UserId);
    }

    [Fact]
    public async Task Create_AllowsSingleDayRange()
    {
        var result = await _service.CreateAsync(new CreateScheduleRequest("Kolokwium", Jan25, Jan25), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RejectsMissingName(string? name)
    {
        var result = await _service.CreateAsync(new CreateScheduleRequest(name, Jan25, Feb14), CancellationToken.None);

        Assert.Equal(ScheduleErrorType.Validation, result.Error!.Type);
        Assert.Empty(_repository.Schedules);
    }

    [Fact]
    public async Task Create_RejectsTooLongName()
    {
        var name = new string('a', Schedule.NameMaxLength + 1);

        var result = await _service.CreateAsync(new CreateScheduleRequest(name, Jan25, Feb14), CancellationToken.None);

        Assert.Equal(ScheduleErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Create_RejectsMissingDates()
    {
        var noStart = await _service.CreateAsync(new CreateScheduleRequest("Sesja", null, Feb14), CancellationToken.None);
        var noEnd = await _service.CreateAsync(new CreateScheduleRequest("Sesja", Jan25, null), CancellationToken.None);

        Assert.Equal(ScheduleErrorType.Validation, noStart.Error!.Type);
        Assert.Equal(ScheduleErrorType.Validation, noEnd.Error!.Type);
    }

    [Fact]
    public async Task Create_RejectsEndBeforeStart()
    {
        var result = await _service.CreateAsync(new CreateScheduleRequest("Sesja", Feb14, Jan25), CancellationToken.None);

        Assert.Equal(ScheduleErrorType.Validation, result.Error!.Type);
        Assert.Empty(_repository.Schedules);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyOwnSchedulesSortedByStartDate()
    {
        await _service.CreateAsync(new CreateScheduleRequest("Sesja zimowa", Jan25, Feb14), CancellationToken.None);
        await _service.CreateAsync(new CreateScheduleRequest("Semestr", new DateOnly(2026, 10, 1), Feb14), CancellationToken.None);
        await _repository.AddAsync(Schedule.Create(Guid.NewGuid(), "Cudzy", Jan25, Feb14, DateTime.UtcNow), CancellationToken.None);

        var result = await _service.GetAllAsync(CancellationToken.None);

        Assert.Equal(["Semestr", "Sesja zimowa"], result.Select(s => s.Name));
    }

    [Fact]
    public async Task Update_WithCurrentVersion_SavesAndIncrementsVersion()
    {
        var created = (await _service.CreateAsync(new CreateScheduleRequest("Sesja", Jan25, Feb14), CancellationToken.None)).Value!;
        _time.Advance(TimeSpan.FromMinutes(5));
        var newEnd = new DateOnly(2027, 2, 20);

        var result = await _service.UpdateAsync(created.Id, new UpdateScheduleRequest("Sesja zimowa", Jan25, newEnd, created.Version), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Sesja zimowa", result.Value!.Name);
        Assert.Equal(newEnd, result.Value.EndDate);
        Assert.Equal(2, result.Value.Version);
        Assert.True(result.Value.UpdatedAt > created.UpdatedAt);
        Assert.Equal(created.CreatedAt, result.Value.CreatedAt);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ReturnsConflictWithCurrentSchedule()
    {
        var created = (await _service.CreateAsync(new CreateScheduleRequest("Sesja", Jan25, Feb14), CancellationToken.None)).Value!;
        await _service.UpdateAsync(created.Id, new UpdateScheduleRequest("Z innej karty", Jan25, Feb14, 1), CancellationToken.None);

        var result = await _service.UpdateAsync(created.Id, new UpdateScheduleRequest("Stara wersja", Jan25, Feb14, 1), CancellationToken.None);

        Assert.Equal(ScheduleErrorType.Conflict, result.Error!.Type);
        Assert.Equal("Z innej karty", result.Error.Current!.Name);
    }

    [Fact]
    public async Task Update_WithoutChanges_DoesNotIncrementVersion()
    {
        var created = (await _service.CreateAsync(new CreateScheduleRequest("Sesja", Jan25, Feb14), CancellationToken.None)).Value!;

        var result = await _service.UpdateAsync(created.Id, new UpdateScheduleRequest(" Sesja ", Jan25, Feb14, 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Version);
    }

    [Fact]
    public async Task Update_RejectsEndBeforeStart()
    {
        var created = (await _service.CreateAsync(new CreateScheduleRequest("Sesja", Jan25, Feb14), CancellationToken.None)).Value!;

        var result = await _service.UpdateAsync(created.Id, new UpdateScheduleRequest("Sesja", Feb14, Jan25, 1), CancellationToken.None);

        Assert.Equal(ScheduleErrorType.Validation, result.Error!.Type);
        Assert.Equal(Feb14, _repository.Schedules.Single().EndDate);
    }

    [Fact]
    public async Task Delete_RemovesSchedule()
    {
        var created = (await _service.CreateAsync(new CreateScheduleRequest("Sesja", Jan25, Feb14), CancellationToken.None)).Value!;

        var result = await _service.DeleteAsync(created.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_repository.Schedules);
    }

    [Fact]
    public async Task ScheduleOfAnotherUser_IsNotFound()
    {
        var foreign = Schedule.Create(Guid.NewGuid(), "Cudzy", Jan25, Feb14, DateTime.UtcNow);
        await _repository.AddAsync(foreign, CancellationToken.None);

        var get = await _service.GetByIdAsync(foreign.Id, CancellationToken.None);
        var update = await _service.UpdateAsync(foreign.Id, new UpdateScheduleRequest("Mój", Jan25, Feb14, 1), CancellationToken.None);
        var delete = await _service.DeleteAsync(foreign.Id, CancellationToken.None);

        Assert.Equal(ScheduleErrorType.NotFound, get.Error!.Type);
        Assert.Equal(ScheduleErrorType.NotFound, update.Error!.Type);
        Assert.Equal(ScheduleErrorType.NotFound, delete.Error!.Type);
        Assert.Equal("Cudzy", _repository.Schedules.Single().Name);
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

    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public List<Schedule> Schedules { get; } = [];

        public Task<IReadOnlyList<Schedule>> GetByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Schedule>>(Schedules.Where(s => s.UserId == userId).ToList());

        public Task<Schedule?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Schedules.FirstOrDefault(s => s.Id == id && s.UserId == userId));

        public Task AddAsync(Schedule schedule, CancellationToken cancellationToken)
        {
            Schedules.Add(schedule);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Schedule schedule, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(Schedule schedule, CancellationToken cancellationToken)
        {
            Schedules.Remove(schedule);
            return Task.CompletedTask;
        }
    }
}
