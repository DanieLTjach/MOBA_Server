using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using MobaSignalRServer.Services;
using Microsoft.AspNetCore.SignalR;

namespace MobaSignalRServer
{
    public class GameServer(WebApplication app, GameStateManager gameStateManager, ILogger<GameServer> logger)
    {
        private readonly ILogger<GameServer> _logger = logger;
        private readonly GameStateManager _gameStateManager = gameStateManager;
        private readonly WebApplication _app = app;
        private Timer? _gameLoopTimer;
        private readonly int _tickRate = 20;

        public void Start()
        {
            _logger.LogInformation("Starting MOBA game server...");

            int tickIntervalMs = 1000 / _tickRate;
            _gameLoopTimer = new Timer(GameLoop, null, 0, tickIntervalMs);
            
            _logger.LogInformation($"Game loop started with tick rate: {_tickRate} ticks per second");

            _app.Run();
        }

        public void Stop()
        {
            _logger.LogInformation("Stopping MOBA game server...");

            _gameLoopTimer?.Dispose();

            _gameStateManager.CleanupAllMatches();
            
            _logger.LogInformation("Game server stopped");
        }

        private void GameLoop(object? state)
        {
            try
            {
                foreach (var matchObj in _gameStateManager.GetAllMatches())
                {
                    if (matchObj is Models.GameMatch match)
                    {
                        if (!match.IsActive)
                            continue;

                        UpdateMatch(match);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in game loop");
            }
        }

        private void UpdateMatch(Models.GameMatch match)
        {
            foreach (var player in match.Players.Values)
            {
                if (!player.IsAlive)
                {
                    var timeSinceDeath = DateTime.UtcNow - player.LastDeathTime;
                    if (timeSinceDeath.TotalSeconds >= 10) 
                    {
                        player.IsAlive = true;
                        player.Health = 100;
                        player.Mana = 100;

                        player.Position = player.TeamId == 1 
                            ? new System.Numerics.Vector2(-100, -100) 
                            : new System.Numerics.Vector2(100, 100);  

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var hubContext = _app.Services.GetRequiredService<IHubContext<MobaGameHub>>();
                                if (!string.IsNullOrEmpty(match.MatchId))
                                {
                                    await hubContext.Clients.Group(match.MatchId).SendAsync("PlayerRespawned", player);
                                }
                                else
                                {
                                    _logger.LogWarning("Cannot notify clients: match.MatchId is null or empty.");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error notifying clients about player respawn");
                            }
                        });
                    }
                }
            }
        }
    }
}