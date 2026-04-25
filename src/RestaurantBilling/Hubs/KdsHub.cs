using Microsoft.AspNetCore.SignalR;

namespace RestaurantBilling.Hubs;

public class KdsHub : Hub
{
    public async Task JoinStation(string stationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"station:{stationId}");
    }
}

