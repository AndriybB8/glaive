namespace StudyPlanner.Application.TodoLists;

public enum TodoListChangeKind
{
    ItemCreated,
    ItemUpdated,
    ItemDeleted,
    ItemsReordered,
    ListRenamed,
    ListDeleted,
    MemberJoined,
    MemberRoleChanged,
    MemberRemoved,
}

/// <summary>
/// Co się zmieniło na liście. To tylko powiadomienie do pokazania („kto co zmienił”) –
/// aktualny stan klient pobiera zwykłymi zapytaniami API, które same sprawdzają dostęp.
/// </summary>
/// <param name="ItemId">Przy zmianach punktu.</param>
/// <param name="ItemTitle">Przy zmianach punktu: jego treść (po usunięciu nie da się jej już pobrać).</param>
/// <param name="ListTitle">Przy zmianach listy i dostępu: jej nazwa (po usunięciu albo utracie dostępu nie da się jej już pobrać).</param>
/// <param name="MemberUserId">Przy zmianach dostępu: osoba, której dotyczą.</param>
public sealed record TodoListChange(
    Guid ListId,
    TodoListChangeKind Kind,
    Guid? ItemId = null,
    string? ItemTitle = null,
    string? ListTitle = null,
    Guid? MemberUserId = null);

/// <summary>
/// Powiadamia połączonych klientów o zmianach, które są już zapisane w bazie.
/// Metody nie zgłaszają błędów: nieudane powiadomienie nie może oznaczać nieudanego zapisu –
/// klient i tak pobierze aktualny stan przy kolejnej zmianie albo po ponownym połączeniu.
/// Nie przyjmują też CancellationToken: przerwane żądanie nie cofa zapisu, więc inni powinni się o nim dowiedzieć.
/// </summary>
public interface ITodoListNotifier
{
    /// <summary>Powiadamia osoby, które mają teraz dostęp do listy: właściciela i osoby po przyjęciu zaproszenia.</summary>
    Task ListChangedAsync(TodoListChange change);

    /// <summary>
    /// Powiadamia wskazane osoby. Gdy listy już nie ma, trzeba je ustalić przed usunięciem.
    /// Zmiany dostępu idą tylko do właściciela i osoby, której dotyczą – pozostali nie widzą, kto ma dostęp.
    /// </summary>
    Task ListChangedAsync(TodoListChange change, IReadOnlyCollection<Guid> recipients);

    /// <summary>Powiadamia zaproszoną osobę, która nie ma jeszcze dostępu do listy.</summary>
    Task InvitationReceivedAsync(Guid invitedUserId, Guid listId, string listTitle);
}
