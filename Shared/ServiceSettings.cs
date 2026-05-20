namespace Shared;

public record ServiceSettings(
    string MainDbConnection,
    Dictionary<string, string> RegionConnections,
    string MqHost,
    string Exchange,
    string Queue,
    string EventsExchange);
