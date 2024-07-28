using SampleProject.Models.Chats;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using SampleProject.Models.ProductInventory;

namespace SampleProject.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IDictionary<string, UserConnection> _connections;

        public ChatHub(IDictionary<string, UserConnection> connections)
        {
            _connections = connections;
        }

        public async Task SendMessage(UserConnection connection, Object message)
        {
            await Clients.Group(connection.Room).SendAsync("NewReceiveMessage", new { user = connection.User, room = connection.Room, message });
        }
        public async Task JoinRoom(UserConnection connection)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection user) == false)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, connection.Room);

                _connections[Context.ConnectionId] = connection;

                await Clients.Group(connection.Room).SendAsync("ReceiveMessage", $"{connection.User} has joined {connection.Room}");
            }
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
