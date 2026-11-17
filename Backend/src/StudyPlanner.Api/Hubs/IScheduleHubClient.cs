using StudyPlanner.Application.Schedules;

namespace StudyPlanner.Api.Hubs;

/// <summary>Wiadomości wysyłane do klientów. Nazwy metod to nazwy zdarzeń po stronie frontendu.</summary>
public interface IScheduleHubClient
{
    Task ScheduleChanged(ScheduleChangedMessage message);

    Task ScheduleInvitationReceived(ScheduleInvitationReceivedMessage message);
}

/// <summary>Po tej wiadomości klient pobiera aktualny stan harmonogramu zwykłymi zapytaniami API.</summary>
public sealed record ScheduleChangedMessage(
    Guid ScheduleId,
    ScheduleChangeKind Change,
    Guid? TaskId,
    string? TaskTitle,
    string? ScheduleName,
    Guid? MemberUserId,
    Guid ActorUserId,
    string ActorEmail,
    DateTime OccurredAt);

/// <summary>Po tej wiadomości klient pobiera aktualne zaproszenia z GET /api/schedule-invitations.</summary>
public sealed record ScheduleInvitationReceivedMessage(
    Guid ScheduleId,
    string ScheduleName,
    Guid ActorUserId,
    string ActorEmail,
    DateTime OccurredAt);
