namespace StudyPlanner.Application.Schedules;

public enum ScheduleChangeKind
{
    TaskCreated,
    TaskUpdated,
    TaskDeleted,
    TaskTagsChanged,
    ScheduleUpdated,
    ScheduleDeleted,
    MemberJoined,
    MemberRoleChanged,
    MemberRemoved,
}

/// <summary>
/// Co się zmieniło w harmonogramie. To tylko powiadomienie do pokazania („kto co zmienił”) –
/// aktualny stan klient pobiera zwykłymi zapytaniami API, które same sprawdzają dostęp.
/// </summary>
/// <param name="TaskId">Przy zmianach zadania.</param>
/// <param name="TaskTitle">Przy zmianach zadania: jego tytuł (po usunięciu nie da się go już pobrać).</param>
/// <param name="ScheduleName">Przy zmianach harmonogramu i dostępu: jego nazwa (po usunięciu albo utracie dostępu nie da się jej już pobrać).</param>
/// <param name="MemberUserId">Przy zmianach dostępu: osoba, której dotyczą.</param>
public sealed record ScheduleChange(
    Guid ScheduleId,
    ScheduleChangeKind Kind,
    Guid? TaskId = null,
    string? TaskTitle = null,
    string? ScheduleName = null,
    Guid? MemberUserId = null);

/// <summary>
/// Powiadamia połączonych klientów o zmianach, które są już zapisane w bazie.
/// Metody nie zgłaszają błędów: nieudane powiadomienie nie może oznaczać nieudanego zapisu –
/// klient i tak pobierze aktualny stan przy kolejnej zmianie albo po ponownym połączeniu.
/// Nie przyjmują też CancellationToken: przerwane żądanie nie cofa zapisu, więc inni powinni się o nim dowiedzieć.
/// </summary>
public interface IScheduleNotifier
{
    /// <summary>Powiadamia osoby, które mają teraz dostęp do harmonogramu: właściciela i osoby po przyjęciu zaproszenia.</summary>
    Task ScheduleChangedAsync(ScheduleChange change);

    /// <summary>
    /// Powiadamia wskazane osoby. Gdy harmonogramu już nie ma, trzeba je ustalić przed usunięciem.
    /// Zmiany dostępu idą tylko do właściciela i osoby, której dotyczą – pozostali nie widzą, kto ma dostęp.
    /// </summary>
    Task ScheduleChangedAsync(ScheduleChange change, IReadOnlyCollection<Guid> recipients);

    /// <summary>Powiadamia zaproszoną osobę, która nie ma jeszcze dostępu do harmonogramu.</summary>
    Task InvitationReceivedAsync(Guid invitedUserId, Guid scheduleId, string scheduleName);
}
