using Microsoft.AspNetCore.SignalR;

namespace StudyPlanner.Api.Hubs;

/// <summary>
/// Powiadomienia o zmianach w harmonogramach na żywo (WebSocket). Tylko serwer wysyła wiadomości, dlatego hub nie ma metod:
/// klient nie zapisuje się do żadnego harmonogramu, a odbiorców wyznacza się przy każdej zmianie na podstawie aktualnego dostępu.
/// Dzięki temu odebranie dostępu działa od razu i nie trzeba nic przywracać po ponownym połączeniu.
/// </summary>
public sealed class ScheduleHub : Hub<IScheduleHubClient>
{
    public const string Path = "/api/hubs/schedules";
}
