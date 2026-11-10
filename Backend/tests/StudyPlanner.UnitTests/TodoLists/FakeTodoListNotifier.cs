using StudyPlanner.Application.TodoLists;

namespace StudyPlanner.UnitTests.TodoLists;

/// <summary>Zapamiętuje wysłane powiadomienia. Recipients = null oznacza „osoby z aktualnym dostępem do listy”.</summary>
internal sealed class FakeTodoListNotifier : ITodoListNotifier
{
    public List<(TodoListChange Change, IReadOnlyCollection<Guid>? Recipients)> Changes { get; } = [];

    public List<(Guid InvitedUserId, Guid ListId, string ListTitle)> Invitations { get; } = [];

    public Task ListChangedAsync(TodoListChange change)
    {
        Changes.Add((change, null));
        return Task.CompletedTask;
    }

    public Task ListChangedAsync(TodoListChange change, IReadOnlyCollection<Guid> recipients)
    {
        Changes.Add((change, recipients));
        return Task.CompletedTask;
    }

    public Task InvitationReceivedAsync(Guid invitedUserId, Guid listId, string listTitle)
    {
        Invitations.Add((invitedUserId, listId, listTitle));
        return Task.CompletedTask;
    }
}
