using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.SignalR;
using StudyPlanner.Application.Common;
using StudyPlanner.Application.Schedules;

namespace StudyPlanner.Api.Hubs;

public sealed class SignalRScheduleNotifier(
    IHubContext<ScheduleHub, IScheduleHubClient> hub,
    IScheduleRepository schedules,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContext,
    TimeProvider timeProvider,
    ILogger<SignalRScheduleNotifier> logger) : IScheduleNotifier
{
    /// <summary>Odpowiedź na zapis czeka na wysłanie, więc wolny klient nie może jej zatrzymać na długo.</summary>
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(2);

    public async Task ScheduleChangedAsync(ScheduleChange change)
    {
        try
        {
            using var timeout = new CancellationTokenSource(SendTimeout);
            var recipients = await schedules.GetUserIdsWithAccessAsync(change.ScheduleId, timeout.Token);
            await SendAsync(change, recipients);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Nie udało się ustalić odbiorców zmiany harmonogramu {ScheduleId}.", change.ScheduleId);
        }
    }

    public async Task ScheduleChangedAsync(ScheduleChange change, IReadOnlyCollection<Guid> recipients)
    {
        try
        {
            await SendAsync(change, recipients);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Nie udało się wysłać zmiany harmonogramu {ScheduleId}.", change.ScheduleId);
        }
    }

    public async Task InvitationReceivedAsync(Guid invitedUserId, Guid scheduleId, string scheduleName)
    {
        try
        {
            var message = new ScheduleInvitationReceivedMessage(scheduleId, scheduleName, currentUser.UserId, ActorEmail(), UtcNow());

            // Typowany klient nie przyjmuje tokenu anulowania; limit czasu pilnuje, żeby nie zatrzymać odpowiedzi.
            await hub.Clients.User(invitedUserId.ToString()).ScheduleInvitationReceived(message).WaitAsync(SendTimeout);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Nie udało się wysłać zaproszenia do harmonogramu {ScheduleId}.", scheduleId);
        }
    }

    private async Task SendAsync(ScheduleChange change, IReadOnlyCollection<Guid> recipients)
    {
        if (recipients.Count == 0)
        {
            return;
        }

        var message = new ScheduleChangedMessage(
            change.ScheduleId,
            change.Kind,
            change.TaskId,
            change.TaskTitle,
            change.ScheduleName,
            change.MemberUserId,
            currentUser.UserId,
            ActorEmail(),
            UtcNow());

        var userIds = recipients.Distinct().Select(id => id.ToString()).ToList();

        // Typowany klient nie przyjmuje tokenu anulowania; limit czasu pilnuje, żeby nie zatrzymać odpowiedzi.
        await hub.Clients.Users(userIds).ScheduleChanged(message).WaitAsync(SendTimeout);
    }

    private string ActorEmail() =>
        httpContext.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? string.Empty;

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
