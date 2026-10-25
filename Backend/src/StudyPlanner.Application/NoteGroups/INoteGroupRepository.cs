using StudyPlanner.Domain.NoteGroups;

namespace StudyPlanner.Application.NoteGroups;

public interface INoteGroupRepository
{
    Task<IReadOnlyList<NoteGroup>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Zwraca grupę tylko wtedy, gdy należy do podanego użytkownika.</summary>
    Task<NoteGroup?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    /// <summary>Czy grupa istnieje i należy do użytkownika (np. przed przeniesieniem do niej notatki).</summary>
    Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    /// <summary>Rzuca <see cref="NoteGroupNameTakenException"/>, gdy użytkownik ma już grupę o tej nazwie.</summary>
    Task AddAsync(NoteGroup group, CancellationToken cancellationToken);

    /// <summary>
    /// Zapisuje zmiany grupy. Rzuca <see cref="NoteGroupConcurrencyException"/>, gdy w międzyczasie ktoś zapisał
    /// nowszą wersję, albo <see cref="NoteGroupNameTakenException"/>, gdy nazwa jest już zajęta.
    /// </summary>
    Task UpdateAsync(NoteGroup group, CancellationToken cancellationToken);

    /// <summary>Usuwa grupę. Jej notatki zostają – baza ustawia im group_id na null („Bez grupy”).</summary>
    Task DeleteAsync(NoteGroup group, CancellationToken cancellationToken);
}

/// <summary>
/// Rzucany przez repozytorium przy konflikcie wersji
/// (w EF Core: przy przechwyceniu DbUpdateConcurrencyException).
/// </summary>
public sealed class NoteGroupConcurrencyException() : Exception("Grupa została zmieniona przez inny zapis.");

/// <summary>
/// Rzucany przez repozytorium, gdy baza odrzuci zapis przez unikalną nazwę grupy.
/// Serwis sprawdza to wcześniej – wyjątek pojawia się tylko przy jednoczesnych zapisach.
/// </summary>
public sealed class NoteGroupNameTakenException() : Exception("Grupa o tej nazwie już istnieje.");
