namespace StudyPlanner.Domain.Schedules;

public enum ScheduleMemberRole
{
    Viewer,
    Editor,
}

public class ScheduleMember
{
    public Guid ScheduleId { get; private set; }
    public Guid UserId { get; private set; }
    public ScheduleMemberRole Role { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }

    private ScheduleMember()
    {
    }

    public static ScheduleMember Invite(Guid scheduleId, Guid userId, ScheduleMemberRole role, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValid(role);

        return new ScheduleMember
        {
            ScheduleId = scheduleId,
            UserId = userId,
            Role = role,
            CreatedAt = utcNow,
            AcceptedAt = null,
        };
    }

    public void Accept(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        AcceptedAt ??= utcNow;
    }

    public void ChangeRole(ScheduleMemberRole role)
    {
        EnsureValid(role);
        Role = role;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Data musi być w UTC.", nameof(value));
    }

    private static void EnsureValid(ScheduleMemberRole role)
    {
        if (!Enum.IsDefined(role))
            throw new ArgumentException("Nieprawidłowe uprawnienia do harmonogramu.", nameof(role));
    }
}
