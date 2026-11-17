using StudyPlanner.Application.Schedules;

namespace StudyPlanner.UnitTests.Schedules;

/// <summary>Zapamiętuje wysłane powiadomienia. Recipients = null oznacza „osoby z aktualnym dostępem do harmonogramu”.</summary>
internal sealed class FakeScheduleNotifier : IScheduleNotifier
{
    public List<(ScheduleChange Change, IReadOnlyCollection<Guid>? Recipients)> Changes { get; } = [];

    public List<(Guid InvitedUserId, Guid ScheduleId, string ScheduleName)> Invitations { get; } = [];

    public Task ScheduleChangedAsync(ScheduleChange change)
    {
        Changes.Add((change, null));
        return Task.CompletedTask;
    }

    public Task ScheduleChangedAsync(ScheduleChange change, IReadOnlyCollection<Guid> recipients)
    {
        Changes.Add((change, recipients));
        return Task.CompletedTask;
    }

    public Task InvitationReceivedAsync(Guid invitedUserId, Guid scheduleId, string scheduleName)
    {
        Invitations.Add((invitedUserId, scheduleId, scheduleName));
        return Task.CompletedTask;
    }
}
