using StackExchange.Redis;

namespace Valuator.Infrastructure;

public class ShardManager : IDisposable
{
    private readonly IConnectionMultiplexer _mainDb;
    private readonly Dictionary<string, IConnectionMultiplexer> _regionDbs;
    private readonly ILogger<ShardManager> _logger;

    public static readonly Dictionary<string, string> CountryRegion = new()
    {
        ["Russia"] = "RU",
        ["France"] = "EU",
        ["Germany"] = "EU",
        ["UAE"] = "ASIA",
        ["India"] = "ASIA"
    };

    public static string[] Countries => CountryRegion.Keys.ToArray();

    public static string GetRegion(string country) => CountryRegion[country];

    public ShardManager(
        string mainConnection,
        Dictionary<string, string> regionConnections,
        ILogger<ShardManager> logger)
    {
        _logger = logger;
        _mainDb = ConnectionMultiplexer.Connect(mainConnection);
        _regionDbs = new Dictionary<string, IConnectionMultiplexer>();
        foreach (var (region, conn) in regionConnections)
        {
            _regionDbs[region] = ConnectionMultiplexer.Connect(conn);
        }
    }

    public IDatabase GetMainDb() => _mainDb.GetDatabase();

    public IDatabase GetRegionDb(string region) => _regionDbs[region].GetDatabase();

    public async Task SetShardAsync(string id, string region)
    {
        var db = GetMainDb();
        await db.StringSetAsync($"SHARD-{id}", region);
    }

    public async Task<string?> GetShardAsync(string id)
    {
        var db = GetMainDb();
        var value = await db.StringGetAsync($"SHARD-{id}");
        if (value.IsNull) return null;
        var region = value.ToString();
        _logger.LogInformation("LOOKUP: {Id},  {Region}", id, region);
        return region;
    }

    public void Dispose()
    {
        _mainDb?.Dispose();
        foreach (var mux in _regionDbs.Values)
            mux?.Dispose();
    }
}
