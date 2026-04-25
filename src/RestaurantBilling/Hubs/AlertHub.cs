using Microsoft.AspNetCore.SignalR;

namespace RestaurantBilling.Hubs;

public class AlertHub : Hub
{
}

public interface IAlertClient
{
    Task DashboardRefresh(string trigger);
}

