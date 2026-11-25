using StudyPlanner.Application.Common;
using StudyPlanner.Application.Schedules;
using StudyPlanner.Domain.Tags;

namespace StudyPlanner.Application.Tags;

public class TagService(ITagRepository repo, IScheduleRepository schedules, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<TagResult<IReadOnlyList<TagDto>>> GetByScheduleAsync(Guid scheduleId,
        CancellationToken cancellationToken)
    {
        if (await schedules.GetWithAccessAsync(scheduleId, currentUser.UserId, cancellationToken) is null)
            return TagResult<IReadOnlyList<TagDto>>.Failure(new TagError(TagErrorType.NotFound, "Nie znaleziono harmonogramu."));

        var tags = await repo.GetByScheduleAsync(scheduleId, cancellationToken);
        IReadOnlyList<TagDto> res = tags
            .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(TagDto.From)
            .ToList();

        return TagResult<IReadOnlyList<TagDto>>.Success(res);
    }

    public async Task<TagResult<TagDto>> GetByIdAsync(Guid id, Guid scheduleId, CancellationToken cancellationToken)
    {
        if (await schedules.GetWithAccessAsync(scheduleId, currentUser.UserId, cancellationToken) is null)
            return TagResult<TagDto>.Failure(new TagError(TagErrorType.NotFound, "Nie znaleziono harmonogramu."));

        var tag = await repo.GetByIdAsync(id, scheduleId, cancellationToken);

        return tag is null
            ? TagResult<TagDto>.Failure(new TagError(TagErrorType.NotFound, "Nie znaleziono tagu."))
            : TagResult<TagDto>.Success(TagDto.From(tag));
    }

    public async Task<TagResult<TagDto>> CreateAsync(Guid scheduleId, CreateTagRequest request, CancellationToken cancellationToken)
    {
        var access = await schedules.GetWithAccessAsync(scheduleId, currentUser.UserId, cancellationToken);
        if (access is null) return TagResult<TagDto>.Failure(new TagError(TagErrorType.NotFound, "Nie znaleziono harmonogramu."));
        if (!access.Access.Can(Permission.EditContent)) return TagResult<TagDto>.Failure(new TagError(TagErrorType.Forbidden, "Masz tylko podgląd tego harmonogramu."));

        var name = request.Name?.Trim() ?? string.Empty;
        var color = request.Color?.Trim() ?? string.Empty;

        if (Validate(name, color) is { } error) return TagResult<TagDto>.Failure(error);

        if (await IsNameTakenAsync(scheduleId, name, exceptId: null, cancellationToken))
            return TagResult<TagDto>.Failure(new TagError(TagErrorType.Validation, "Tag o tej nazwie już istnieje."));

        var tag = Tag.Create(scheduleId, name, color, UtcNow());

        try
        {
            await repo.AddAsync(tag, cancellationToken);
        }
        catch (TagNameTakenException)
        {
            return TagResult<TagDto>.Failure(new TagError(TagErrorType.Validation, "Tag o tej nazwie już istnieje."));
        }

        return TagResult<TagDto>.Success(TagDto.From(tag));
    }

    public async Task<TagResult<TagDto>> UpdateAsync(Guid scheduleId, Guid id, UpdateTagRequest req,
        CancellationToken cancellationToken)
    {
        var access = await schedules.GetWithAccessAsync(scheduleId, currentUser.UserId, cancellationToken);
        if (access is null) return TagResult<TagDto>.Failure(new TagError(TagErrorType.NotFound, "Nie znaleziono harmonogramu."));
        if (!access.Access.Can(Permission.EditContent)) return TagResult<TagDto>.Failure(new TagError(TagErrorType.Forbidden, "Masz tylko podgląd tego harmonogramu."));

        var name = req.Name?.Trim() ?? string.Empty;
        var color = req.Color?.Trim() ?? string.Empty;

        if (Validate(name, color) is { } error) return TagResult<TagDto>.Failure(error);

        var tag = await repo.GetByIdAsync(id, scheduleId, cancellationToken);
        if (tag is null) return TagResult<TagDto>.Failure(new TagError(TagErrorType.NotFound, "Nie znaleziono tagu."));

        if (tag.Version != req.Version) return TagResult<TagDto>.Failure(Conflict(tag));

        if (tag.Name == name && tag.Color == color) return TagResult<TagDto>.Success(TagDto.From(tag));

        if (await IsNameTakenAsync(scheduleId, name, exceptId: tag.Id, cancellationToken))
            return TagResult<TagDto>.Failure(new TagError(TagErrorType.Validation, "Tag o tej nazwie już istnieje."));

        tag.Update(name, color, UtcNow());

        try
        {
            await repo.UpdateAsync(tag, cancellationToken);
        }
        catch (TagNameTakenException)
        {
            return TagResult<TagDto>.Failure(new TagError(TagErrorType.Validation, "Tag o tej nazwie już istnieje."));
        }
        catch (TagConcurrencyException)
        {
            var current = await repo.GetByIdAsync(id, scheduleId, cancellationToken);
            return current is null
                ? TagResult<TagDto>.Failure(new TagError(TagErrorType.NotFound, "Nie znaleziono tagu."))
                : TagResult<TagDto>.Failure(Conflict(current));
        }

        return TagResult<TagDto>.Success(TagDto.From(tag));
    }

    public async Task<TagResult<bool>> DeleteAsync(Guid scheduleId, Guid id, CancellationToken cancellationToken)
    {
        var access = await schedules.GetWithAccessAsync(scheduleId, currentUser.UserId, cancellationToken);
        if (access is null) return TagResult<bool>.Failure(new TagError(TagErrorType.NotFound, "Nie znaleziono harmonogramu."));
        if (!access.Access.Can(Permission.EditContent)) return TagResult<bool>.Failure(new TagError(TagErrorType.Forbidden, "Masz tylko podgląd tego harmonogramu."));

        var tag = await repo.GetByIdAsync(id, scheduleId, cancellationToken);
        if (tag is null)
            return TagResult<bool>.Failure(new TagError(TagErrorType.NotFound, "Nie znaleziono tagu."));

        await repo.DeleteAsync(tag, cancellationToken);
        return TagResult<bool>.Success(true);
    }

    private async Task<bool> IsNameTakenAsync(Guid scheduleId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var tags = await repo.GetByScheduleAsync(scheduleId, cancellationToken);
        return tags.Any(t => t.Id != exceptId && string.Equals(t.Name, name, StringComparison.CurrentCultureIgnoreCase));
    }

    private static TagError Conflict(Tag current) =>
        new(TagErrorType.Conflict, "Tag został w międzyczasie zmieniony w innym miejscu.", TagDto.From(current));

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static TagError? Validate(string name, string color)
    {
        if (name.Length == 0) return new TagError(TagErrorType.Validation, "Podaj nazwę tagu.");
        if (name.Length > Tag.NameMaxLength)
            return new TagError(TagErrorType.Validation,
                $"Nazwa tagu może mieć maksymalnie {Tag.NameMaxLength} znaków. Podano {name.Length}.");
        if (!Tag.IsValidColor(color))
            return new TagError(TagErrorType.Validation, "Kolor musi być w formacie szesnastkowym, np. #12ABC9");


        return null;
    }
}
