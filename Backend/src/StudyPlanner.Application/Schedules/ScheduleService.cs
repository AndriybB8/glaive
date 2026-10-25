using StudyPlanner.Application.Common;
using StudyPlanner.Domain.Schedules;

namespace StudyPlanner.Application.Schedules;

public sealed class ScheduleService(IScheduleRepository repository, ICurrentUser currentUser, TimeProvider timeProvider)
{
    private static readonly ScheduleError NotFound = new(ScheduleErrorType.NotFound, "Nie znaleziono harmonogramu.");

    /// <summary>Harmonogramy posortowane od najwcześniej zaczynających się.</summary>
    public async Task<IReadOnlyList<ScheduleDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var schedules = await repository.GetByUserAsync(currentUser.UserId, cancellationToken);

        return schedules
            .OrderBy(s => s.StartDate)
            .ThenBy(s => s.EndDate)
            .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(ScheduleDto.From)
            .ToList();
    }

    public async Task<ScheduleResult<ScheduleDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var schedule = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);

        return schedule is null
            ? ScheduleResult<ScheduleDto>.Failure(NotFound)
            : ScheduleResult<ScheduleDto>.Success(ScheduleDto.From(schedule));
    }

    public async Task<ScheduleResult<ScheduleDto>> CreateAsync(CreateScheduleRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;

        if (Validate(name, request.StartDate, request.EndDate) is { } error)
        {
            return ScheduleResult<ScheduleDto>.Failure(error);
        }

        var schedule = Schedule.Create(currentUser.UserId, name, request.StartDate!.Value, request.EndDate!.Value, UtcNow());
        await repository.AddAsync(schedule, cancellationToken);

        return ScheduleResult<ScheduleDto>.Success(ScheduleDto.From(schedule));
    }

    public async Task<ScheduleResult<ScheduleDto>> UpdateAsync(Guid id, UpdateScheduleRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;

        if (Validate(name, request.StartDate, request.EndDate) is { } error)
        {
            return ScheduleResult<ScheduleDto>.Failure(error);
        }

        var startDate = request.StartDate!.Value;
        var endDate = request.EndDate!.Value;

        var schedule = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (schedule is null)
        {
            return ScheduleResult<ScheduleDto>.Failure(NotFound);
        }

        if (schedule.Version != request.Version)
        {
            return ScheduleResult<ScheduleDto>.Failure(Conflict(schedule));
        }

        // Zapis bez zmian (np. kliknięcie „Zapisz” bez edycji) nie podbija wersji ani daty.
        if (schedule.Name == name && schedule.StartDate == startDate && schedule.EndDate == endDate)
        {
            return ScheduleResult<ScheduleDto>.Success(ScheduleDto.From(schedule));
        }

        schedule.Update(name, startDate, endDate, UtcNow());

        try
        {
            await repository.UpdateAsync(schedule, cancellationToken);
        }
        catch (ScheduleConcurrencyException)
        {
            var current = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
            return current is null
                ? ScheduleResult<ScheduleDto>.Failure(NotFound)
                : ScheduleResult<ScheduleDto>.Failure(Conflict(current));
        }

        return ScheduleResult<ScheduleDto>.Success(ScheduleDto.From(schedule));
    }

    public async Task<ScheduleResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var schedule = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (schedule is null)
        {
            return ScheduleResult<bool>.Failure(NotFound);
        }

        await repository.DeleteAsync(schedule, cancellationToken);
        return ScheduleResult<bool>.Success(true);
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static ScheduleError Conflict(Schedule current) =>
        new(ScheduleErrorType.Conflict, "Harmonogram został w międzyczasie zmieniony w innym miejscu.", ScheduleDto.From(current));

    private static ScheduleError? Validate(string name, DateOnly? startDate, DateOnly? endDate)
    {
        if (name.Length == 0)
        {
            return Invalid("Podaj nazwę harmonogramu.");
        }

        if (name.Length > Schedule.NameMaxLength)
        {
            return Invalid($"Nazwa może mieć maksymalnie {Schedule.NameMaxLength} znaków.");
        }

        if (startDate is null)
        {
            return Invalid("Podaj datę rozpoczęcia.");
        }

        if (endDate is null)
        {
            return Invalid("Podaj datę zakończenia.");
        }

        if (endDate < startDate)
        {
            return Invalid("Data zakończenia nie może być wcześniejsza niż data rozpoczęcia.");
        }

        return null;
    }

    private static ScheduleError Invalid(string message) => new(ScheduleErrorType.Validation, message);
}
