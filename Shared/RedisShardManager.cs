using StackExchange.Redis;

namespace Shared;

public class RedisShardManager : IDisposable
{
    private readonly IConnectionMultiplexer _mainDb;
    private readonly Dictionary<string, IConnectionMultiplexer> _regionDbs;

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

    public RedisShardManager(string mainConnection, Dictionary<string, string> regionConnections)
    {
        _mainDb = ConnectWithRetry(mainConnection);
        _regionDbs = new Dictionary<string, IConnectionMultiplexer>();
        foreach (var (region, conn) in regionConnections)
        {
            _regionDbs[region] = ConnectWithRetry(conn);
        }
    }

    private static ConnectionMultiplexer ConnectWithRetry(string connection)
    {
        return ConnectionMultiplexer.Connect($"{connection},abortConnect=false");
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
        return value.IsNull ? null : value.ToString();
    }

    public void Dispose()
    {
        _mainDb?.Dispose();
        foreach (var mux in _regionDbs.Values)
            mux?.Dispose();
    }
}
