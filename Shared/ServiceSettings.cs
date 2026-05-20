namespace Shared;

public class ServiceSettings
{
    public string MainDbConnection { get; init; }
    public Dictionary<string, string> RegionConnections { get; init; }
    public string MqHost { get; init; }
    public string Exchange { get; init; }
    public string Queue { get; init; }
    public string EventsExchange { get; init; }

    public ServiceSettings(
        string mainDbConnection,
        Dictionary<string, string> regionConnections,
        string mqHost,
        string exchange,
        string queue,
        string eventsExchange)
    {
        MainDbConnection = mainDbConnection;
        RegionConnections = regionConnections;
        MqHost = mqHost;
        Exchange = exchange;
        Queue = queue;
        EventsExchange = eventsExchange;
    }
}
