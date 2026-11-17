using StudyPlanner.Application.Common;
using StudyPlanner.Domain.Schedules;

namespace StudyPlanner.Application.Schedules;

public sealed class ScheduleService(IScheduleRepository repository, ICurrentUser currentUser, TimeProvider timeProvider)
{
    private static readonly ScheduleError NotFound = new(ScheduleErrorType.NotFound, "Nie znaleziono harmonogramu.");
    private static readonly ScheduleError Forbidden = new(ScheduleErrorType.Forbidden, "Tylko właściciel może zmienić harmonogram albo go usunąć.");

    /// <summary>
    /// Własne harmonogramy i wspólne (po przyjęciu zaproszenia), posortowane od najwcześniej zaczynających się.
    /// </summary>
    public async Task<IReadOnlyList<ScheduleDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var schedules = await repository.GetAccessibleByUserAsync(currentUser.UserId, cancellationToken);

        return schedules
            .OrderBy(s => s.Schedule.StartDate)
            .ThenBy(s => s.Schedule.EndDate)
            .ThenBy(s => s.Schedule.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => ScheduleDto.From(s.Schedule, s.Access))
            .ToList();
    }

    /// <summary>Także dla członka wspólnego harmonogramu – zmiana nazwy, dat i usuwanie zostają tylko dla właściciela.</summary>
    public async Task<ScheduleResult<ScheduleDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await repository.GetWithAccessAsync(id, currentUser.UserId, cancellationToken);

        return found is null
            ? ScheduleResult<ScheduleDto>.Failure(NotFound)
            : ScheduleResult<ScheduleDto>.Success(ScheduleDto.From(found.Schedule, found.Access));
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

    /// <summary>Zmiana nazwy i dat – tylko właściciel.</summary>
    public async Task<ScheduleResult<ScheduleDto>> UpdateAsync(Guid id, UpdateScheduleRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;

        if (Validate(name, request.StartDate, request.EndDate) is { } error)
        {
            return ScheduleResult<ScheduleDto>.Failure(error);
        }

        var startDate = request.StartDate!.Value;
        var endDate = request.EndDate!.Value;

        var found = await repository.GetWithAccessAsync(id, currentUser.UserId, cancellationToken);
        if (found is null)
        {
            return ScheduleResult<ScheduleDto>.Failure(NotFound);
        }

        if (!found.Access.Can(Permission.Manage))
        {
            return ScheduleResult<ScheduleDto>.Failure(Forbidden);
        }

        var schedule = found.Schedule;

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
            var current = await repository.GetWithAccessAsync(id, currentUser.UserId, cancellationToken);
            return current is null
                ? ScheduleResult<ScheduleDto>.Failure(NotFound)
                : ScheduleResult<ScheduleDto>.Failure(Conflict(current.Schedule));
        }

        return ScheduleResult<ScheduleDto>.Success(ScheduleDto.From(schedule));
    }

    /// <summary>Usuwa harmonogram razem z zadaniami i osobami z dostępem – tylko właściciel.</summary>
    public async Task<ScheduleResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await repository.GetWithAccessAsync(id, currentUser.UserId, cancellationToken);
        if (found is null)
        {
            return ScheduleResult<bool>.Failure(NotFound);
        }

        if (!found.Access.Can(Permission.Manage))
        {
            return ScheduleResult<bool>.Failure(Forbidden);
        }

        await repository.DeleteAsync(found.Schedule, cancellationToken);
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
