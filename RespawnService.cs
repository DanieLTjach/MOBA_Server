using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MobaSignalRServer.Services;
using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace MobaSignalRServer
{
    public class RespawnService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RespawnService> _logger;
        private readonly ConcurrentQueue<RespawnTask> _respawnQueue = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public RespawnService(IServiceProvider serviceProvider, ILogger<RespawnService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task ScheduleRespawn(string playerConnectionId, string matchId, TimeSpan delay)
        {
            var respawnTask = new RespawnTask
            {
                PlayerConnectionId = playerConnectionId,
                MatchId = matchId,
                RespawnTime = DateTime.UtcNow.Add(delay)
            };

            _respawnQueue.Enqueue(respawnTask);
            _logger.LogInformation($"⏰ Scheduled respawn for player {playerConnectionId} at {respawnTask.RespawnTime}");
            return Task.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 RespawnService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessRespawnQueue();
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error in RespawnService: {ex.Message}");
                }
            }

            _logger.LogInformation("🛑 RespawnService stopped");
        }

        private async Task ProcessRespawnQueue()
        {
            if (_respawnQueue.IsEmpty) return;

            await _semaphore.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                var tasksToProcess = new List<RespawnTask>();

                // Collect all tasks that are ready for respawn
                while (_respawnQueue.TryDequeue(out var task))
                {
                    if (task.RespawnTime <= now)
                    {
                        tasksToProcess.Add(task);
                    }
                    else
                    {
                        // Put it back if not ready yet
                        _respawnQueue.Enqueue(task);
                        break;
                    }
                }

                // Process ready tasks
                foreach (var task in tasksToProcess)
                {
                    await ProcessRespawn(task);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task ProcessRespawn(RespawnTask task)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var gameStateManager = scope.ServiceProvider.GetRequiredService<GameStateManager>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<MobaGameHub>>();

                var match = gameStateManager.GetMatch(task.MatchId);
                if (match == null)
                {
                    _logger.LogWarning($"❌ Match {task.MatchId} not found for respawn");
                    return;
                }

                if (!match.Players.TryGetValue(task.PlayerConnectionId, out var player))
                {
                    _logger.LogWarning($"❌ Player {task.PlayerConnectionId} not found in match {task.MatchId}");
                    return;
                }

                // Respawn the player
                player.IsAlive = true;
                player.Health = 100;
                player.Mana = 100;

                // Set spawn position based on team
                if (player.TeamId == 1)
                    player.Position = new Vector2(-104.2f, -97.6f);
                else if (player.TeamId == 2)
                    player.Position = new Vector2(80.8f, 91.3f);
                else
                    player.Position = new Vector2(0f, 0f);

                player.LastUpdateTime = DateTime.UtcNow;

                // Send respawn data to clients
                var respawnData = new PlayerDataDto(player);
                await hubContext.Clients.Group(task.MatchId).SendAsync("PlayerRespawned", respawnData);

                _logger.LogInformation($"🌀 Player {player.Username} successfully respawned at position ({player.Position.X}, {player.Position.Y})");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error processing respawn for player {task.PlayerConnectionId}: {ex.Message}");
            }
        }

        public override void Dispose()
        {
            _semaphore?.Dispose();
            base.Dispose();
        }
    }

    public class RespawnTask
    {
        public required string PlayerConnectionId { get; set; }
        public required string MatchId { get; set; }
        public DateTime RespawnTime { get; set; }
    }
}