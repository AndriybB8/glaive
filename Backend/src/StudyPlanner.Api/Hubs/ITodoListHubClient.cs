using StudyPlanner.Application.TodoLists;

namespace StudyPlanner.Api.Hubs;

/// <summary>Wiadomości wysyłane do klientów. Nazwy metod to nazwy zdarzeń po stronie frontendu.</summary>
public interface ITodoListHubClient
{
    Task TodoListChanged(TodoListChangedMessage message);

    Task TodoListInvitationReceived(TodoListInvitationReceivedMessage message);
}

/// <summary>Po tej wiadomości klient pobiera aktualny stan listy zwykłymi zapytaniami API.</summary>
public sealed record TodoListChangedMessage(
    Guid ListId,
    TodoListChangeKind Change,
    Guid? ItemId,
    string? ItemTitle,
    string? ListTitle,
    Guid? MemberUserId,
    Guid ActorUserId,
    string ActorEmail,
    DateTime OccurredAt);

/// <summary>Po tej wiadomości klient pobiera aktualne zaproszenia z GET /api/todo-list-invitations.</summary>
public sealed record TodoListInvitationReceivedMessage(
    Guid ListId,
    string ListTitle,
    Guid ActorUserId,
    string ActorEmail,
    DateTime OccurredAt);
