using API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR;

/// <summary>
/// Only authorized users should be able to access
/// WebSockets is the protocol used by SignalR
/// We will not have access to http headers so we have to do slightly different way
/// </summary>
[Authorize]
public class PresenceHub : Hub
{
    private readonly PresenceTracker _tracker;

    public PresenceHub(PresenceTracker tracker)
    {
        _tracker = tracker;
    }

    public override async Task OnConnectedAsync()
    {
        await _tracker.UserConnected(Context.User.GetUsername(), Context.ConnectionId);
        // Clients = connected to the presence hub will be notified
        await Clients.Others.SendAsync("UserIsOnline", Context.User.GetUsername());

        var currentUsers = await _tracker.GetOnlineUsers();
        // Caller means only calling client
        await Clients.Caller.SendAsync("GetOnlineUsers", currentUsers);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        await _tracker.UserDisconnected(Context.User.GetUsername(), Context.ConnectionId);

        // Clients = connected to the presence hub will be notified when user goes offline
        await Clients.Others.SendAsync("UserIsOffline", Context.User.GetUsername());

        // var currentUsers = await _tracker.GetOnlineUsers();
        // await Clients.All.SendAsync("GetOnlineUsers", currentUsers);

        await base.OnDisconnectedAsync(exception);
    }
}
