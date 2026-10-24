using StudyPlanner.Application.Common;
using StudyPlanner.Domain.Notes;

namespace StudyPlanner.Application.Notes;

public sealed class NoteService(INoteRepository repository, ICurrentUser currentUser, TimeProvider timeProvider)
{
    private static readonly NoteError NotFound = new(NoteErrorType.NotFound, "Nie znaleziono notatki.");

    public async Task<IReadOnlyList<NoteSummaryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var notes = await repository.GetByAuthorAsync(currentUser.UserId, cancellationToken);

        return notes
            .OrderByDescending(n => n.UpdatedAt)
            .Select(NoteSummaryDto.From)
            .ToList();
    }

    public async Task<NoteResult<NoteDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var note = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);

        return note is null
            ? NoteResult<NoteDto>.Failure(NotFound)
            : NoteResult<NoteDto>.Success(NoteDto.From(note));
    }

    public async Task<NoteResult<NoteDto>> CreateAsync(CreateNoteRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        var content = request.Content ?? string.Empty;

        if (Validate(title, content) is { } error)
        {
            return NoteResult<NoteDto>.Failure(error);
        }

        var note = Note.Create(currentUser.UserId, title, content, UtcNow());
        await repository.AddAsync(note, cancellationToken);

        return NoteResult<NoteDto>.Success(NoteDto.From(note));
    }

    public async Task<NoteResult<NoteDto>> UpdateAsync(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        var content = request.Content ?? string.Empty;

        if (Validate(title, content) is { } error)
        {
            return NoteResult<NoteDto>.Failure(error);
        }

        var note = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (note is null)
        {
            return NoteResult<NoteDto>.Failure(NotFound);
        }

        if (note.Version != request.Version)
        {
            return NoteResult<NoteDto>.Failure(Conflict(note));
        }

        // Autozapis może wysłać niezmienioną treść – wtedy nie podbijamy wersji ani daty.
        if (note.Title == title && note.Content == content)
        {
            return NoteResult<NoteDto>.Success(NoteDto.From(note));
        }

        note.Update(title, content, UtcNow());

        try
        {
            await repository.UpdateAsync(note, cancellationToken);
        }
        catch (NoteConcurrencyException)
        {
            var current = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
            return current is null
                ? NoteResult<NoteDto>.Failure(NotFound)
                : NoteResult<NoteDto>.Failure(Conflict(current));
        }

        return NoteResult<NoteDto>.Success(NoteDto.From(note));
    }

    public async Task<NoteResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var note = await repository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (note is null)
        {
            return NoteResult<bool>.Failure(NotFound);
        }

        await repository.DeleteAsync(note, cancellationToken);
        return NoteResult<bool>.Success(true);
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static NoteError Conflict(Note current) =>
        new(NoteErrorType.Conflict, "Notatka została w międzyczasie zmieniona w innym miejscu.", NoteDto.From(current));

    private static NoteError? Validate(string title, string content)
    {
        if (title.Length > Note.TitleMaxLength)
        {
            return new NoteError(NoteErrorType.Validation, $"Tytuł może mieć maksymalnie {Note.TitleMaxLength} znaków.");
        }

        if (content.Length > Note.ContentMaxLength)
        {
            return new NoteError(NoteErrorType.Validation, "Notatka jest za długa.");
        }

        return null;
    }
}
