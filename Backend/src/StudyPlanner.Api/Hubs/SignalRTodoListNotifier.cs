using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.SignalR;
using StudyPlanner.Application.Common;
using StudyPlanner.Application.TodoLists;

namespace StudyPlanner.Api.Hubs;

public sealed class SignalRTodoListNotifier(
    IHubContext<TodoListHub, ITodoListHubClient> hub,
    ITodoListRepository lists,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContext,
    TimeProvider timeProvider,
    ILogger<SignalRTodoListNotifier> logger) : ITodoListNotifier
{
    /// <summary>Odpowiedź na zapis czeka na wysłanie, więc wolny klient nie może jej zatrzymać na długo.</summary>
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(2);

    public async Task ListChangedAsync(TodoListChange change)
    {
        try
        {
            using var timeout = new CancellationTokenSource(SendTimeout);
            var recipients = await lists.GetUserIdsWithAccessAsync(change.ListId, timeout.Token);
            await SendAsync(change, recipients);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Nie udało się ustalić odbiorców zmiany listy {ListId}.", change.ListId);
        }
    }

    public async Task ListChangedAsync(TodoListChange change, IReadOnlyCollection<Guid> recipients)
    {
        try
        {
            await SendAsync(change, recipients);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Nie udało się wysłać zmiany listy {ListId}.", change.ListId);
        }
    }

    public async Task InvitationReceivedAsync(Guid invitedUserId, Guid listId, string listTitle)
    {
        try
        {
            var message = new TodoListInvitationReceivedMessage(listId, listTitle, currentUser.UserId, ActorEmail(), UtcNow());

            // Typowany klient nie przyjmuje tokenu anulowania; limit czasu pilnuje, żeby nie zatrzymać odpowiedzi.
            await hub.Clients.User(invitedUserId.ToString()).TodoListInvitationReceived(message).WaitAsync(SendTimeout);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Nie udało się wysłać zaproszenia do listy {ListId}.", listId);
        }
    }

    private async Task SendAsync(TodoListChange change, IReadOnlyCollection<Guid> recipients)
    {
        if (recipients.Count == 0)
        {
            return;
        }

        var message = new TodoListChangedMessage(
            change.ListId,
            change.Kind,
            change.ItemId,
            change.ItemTitle,
            change.ListTitle,
            change.MemberUserId,
            currentUser.UserId,
            ActorEmail(),
            UtcNow());

        var userIds = recipients.Distinct().Select(id => id.ToString()).ToList();

        // Typowany klient nie przyjmuje tokenu anulowania; limit czasu pilnuje, żeby nie zatrzymać odpowiedzi.
        await hub.Clients.Users(userIds).TodoListChanged(message).WaitAsync(SendTimeout);
    }

    private string ActorEmail() =>
        httpContext.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? string.Empty;

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
