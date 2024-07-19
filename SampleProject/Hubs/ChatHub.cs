using SampleProject.Models.Chats;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;

namespace SampleProject.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IDictionary<string, UserConnection> _connections;

        public ChatHub(IDictionary<string, UserConnection> connections)
        {
            _connections = connections;
        }

        public async Task SendMessage(string message)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection connection))
            {
                await Clients.Group(connection.Room).SendAsync("ReceiveMessage", message);
            }
        }
        public async Task JoinRoom(UserConnection connection)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, connection.Room);

            _connections[Context.ConnectionId] = connection;

            await Clients.Group(connection.Room).SendAsync("ReceiveMessage", $"{connection.User} has joined {connection.Room}");
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                _connections.Remove(Context.ConnectionId);
                Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", $"{userConnection.User} has left");
                
            }

            return base.OnDisconnectedAsync(exception);
        }

    }
}
