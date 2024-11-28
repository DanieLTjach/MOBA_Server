using Microsoft.AspNetCore.SignalR;
using MMOServer;
using System;
using System.Threading.Tasks;

namespace SignalRChat.Hubs
{
    public class GameHub : Hub
    {

        private readonly Server _server;
        private bool gameStarted = false;
        private CancellationTokenSource _cancellationTokenSource;

        public GameHub(Server server)
        {
            _server = server;
        }

        // Connection methods
        // bool gameStarted = false;
        public async Task Connect(string user)
        {
            await Clients.All.SendAsync("UserConnected", user);
            await _server.AddPlayer(user);
        }
        public async Task Disconnect(string user)
        {
            await Clients.All.SendAsync("UserDisconnected", user);
            await _server.RemovePlayer(user);
        }

        public async Task GetActualBlueTeam()
        {
            await Clients.All.SendAsync("ActualBlueTeam", _server.team_blue);
        }

        public async Task GetActualRedTeam()
        {
            await Clients.All.SendAsync("ActualRedTeam", _server.team_red);
        }
        // Movement methods

    //    private CancellationTokenSource _cancellationTokenSource;

    public async Task StartGame()
    {   
    gameStarted = true;
    _server.is_gameStarted = true;
    _cancellationTokenSource = new CancellationTokenSource();

    await Clients.All.SendAsync("UpdatePlayerCoordinates", _server.team_blue, _server.team_red);
    }

    public async Task StopGame()
    {
        gameStarted = false;
        _server.is_gameStarted = false;
        _cancellationTokenSource?.Cancel(); // Отмена задержки
    }


        public async Task MoveTask(string user, int TargetX, int TargetY)
        {
            await _server.MovePlayer(user, TargetX, TargetY);
        }



        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var user = Context.ConnectionId; // Assuming user is identified by connection ID
            await Disconnect(user);
            await base.OnDisconnectedAsync(exception);
        }
    }
}