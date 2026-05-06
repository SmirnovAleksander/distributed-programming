using Microsoft.AspNetCore.SignalR;

namespace Valuator.Hubs;

public class RankUpdatesHub : Hub
{
    public Task SubscribeToSummary(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.CompletedTask;
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, id);
    }
}
