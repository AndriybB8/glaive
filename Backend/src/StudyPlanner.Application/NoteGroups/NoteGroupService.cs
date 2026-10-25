using StudyPlanner.Application.Common;
using StudyPlanner.Domain.NoteGroups;

namespace StudyPlanner.Application.NoteGroups;

public sealed class NoteGroupService(INoteGroupRepository repository, ICurrentUser currentUser, TimeProvider timeProvider)
{
    private static readonly NoteGroupError NotFound = new(NoteGroupErrorType.NotFound, "Nie znaleziono grupy.");
    private static readonly NoteGroupError NameTaken = Invalid("Grupa o tej nazwie już istnieje.");

    /// <summary>Grupy użytkownika posortowane alfabetycznie.</summary>
    public async Task<IReadOnlyList<NoteGroupDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var groups = await repository.GetByUserAsync(currentUser.UserId, cancellationToken);

        return groups
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(NoteGroupDto.From)
            .ToList();
    }

    public async Task<NoteGroupResult<NoteGroupDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var group = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);

        return group is null
            ? NoteGroupResult<NoteGroupDto>.Failure(NotFound)
            : NoteGroupResult<NoteGroupDto>.Success(NoteGroupDto.From(group));
    }

    public async Task<NoteGroupResult<NoteGroupDto>> CreateAsync(CreateNoteGroupRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;

        if (Validate(name) is { } error)
        {
            return NoteGroupResult<NoteGroupDto>.Failure(error);
        }

        if (await IsNameTakenAsync(name, exceptId: null, cancellationToken))
        {
            return NoteGroupResult<NoteGroupDto>.Failure(NameTaken);
        }

        var group = NoteGroup.Create(currentUser.UserId, name, UtcNow());

        try
        {
            await repository.AddAsync(group, cancellationToken);
        }
        catch (NoteGroupNameTakenException)
        {
            return NoteGroupResult<NoteGroupDto>.Failure(NameTaken);
        }

        return NoteGroupResult<NoteGroupDto>.Success(NoteGroupDto.From(group));
    }

    /// <summary>Zmiana nazwy grupy.</summary>
    public async Task<NoteGroupResult<NoteGroupDto>> UpdateAsync(Guid id, UpdateNoteGroupRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;

        if (Validate(name) is { } error)
        {
            return NoteGroupResult<NoteGroupDto>.Failure(error);
        }

        var group = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (group is null)
        {
            return NoteGroupResult<NoteGroupDto>.Failure(NotFound);
        }

        if (group.Version != request.Version)
        {
            return NoteGroupResult<NoteGroupDto>.Failure(Conflict(group));
        }

        // Zapis bez zmian (np. „Zapisz” bez edycji) nie podbija wersji ani daty.
        if (group.Name == name)
        {
            return NoteGroupResult<NoteGroupDto>.Success(NoteGroupDto.From(group));
        }

        // Sama zmiana wielkości liter („matma” → „Matma”) jest dozwolona – pomijamy tę grupę przy porównaniu.
        if (await IsNameTakenAsync(name, exceptId: group.Id, cancellationToken))
        {
            return NoteGroupResult<NoteGroupDto>.Failure(NameTaken);
        }

        group.Rename(name, UtcNow());

        try
        {
            await repository.UpdateAsync(group, cancellationToken);
        }
        catch (NoteGroupNameTakenException)
        {
            return NoteGroupResult<NoteGroupDto>.Failure(NameTaken);
        }
        catch (NoteGroupConcurrencyException)
        {
            var current = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
            return current is null
                ? NoteGroupResult<NoteGroupDto>.Failure(NotFound)
                : NoteGroupResult<NoteGroupDto>.Failure(Conflict(current));
        }

        return NoteGroupResult<NoteGroupDto>.Success(NoteGroupDto.From(group));
    }

    /// <summary>Usuwa grupę. Notatki z tej grupy nie są usuwane – trafiają do „Bez grupy”.</summary>
    public async Task<NoteGroupResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var group = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (group is null)
        {
            return NoteGroupResult<bool>.Failure(NotFound);
        }

        await repository.DeleteAsync(group, cancellationToken);
        return NoteGroupResult<bool>.Success(true);
    }

    private async Task<bool> IsNameTakenAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var groups = await repository.GetByUserAsync(currentUser.UserId, cancellationToken);

        return groups.Any(g => g.Id != exceptId && string.Equals(g.Name, name, StringComparison.CurrentCultureIgnoreCase));
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static NoteGroupError Conflict(NoteGroup current) =>
        new(NoteGroupErrorType.Conflict, "Grupa została w międzyczasie zmieniona w innym miejscu.", NoteGroupDto.From(current));

    private static NoteGroupError? Validate(string name)
    {
        if (name.Length == 0)
        {
            return Invalid("Podaj nazwę grupy.");
        }

        if (name.Length > NoteGroup.NameMaxLength)
        {
            return Invalid($"Nazwa grupy może mieć maksymalnie {NoteGroup.NameMaxLength} znaków.");
        }

        return null;
    }

    private static NoteGroupError Invalid(string message) => new(NoteGroupErrorType.Validation, message);
}
