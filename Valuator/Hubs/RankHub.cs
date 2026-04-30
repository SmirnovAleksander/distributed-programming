using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace Valuator.Hubs;

public class RankHub : Hub
{
    private readonly IConnectionMultiplexer _redis;

    public RankHub(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public static string GroupFor(string recordId) => $"rank-{recordId}";

    public async Task Subscribe(string recordId)
    {
        if (string.IsNullOrWhiteSpace(recordId))
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(recordId));
    }

    public Task<double?> GetRankIfReady(string recordId)
    {
        if (string.IsNullOrWhiteSpace(recordId))
            return Task.FromResult<double?>(null);

        var raw = _redis.GetDatabase().StringGet($"RANK-{recordId}");
        return Task.FromResult(raw.IsNull ? null : (double?)raw);
    }
}
