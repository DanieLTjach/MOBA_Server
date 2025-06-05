using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using Microsoft.Extensions.Logging;
using MobaSignalRServer.Models.Heroes;


namespace MobaSignalRServer
{
    
public enum MatchState
{
    Waiting,
    InProgress,
    Finished
}

    public class Player
    {
        public string Username { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
         public int HeroId { get; set; }
        public int TeamId { get; set; }
        public float Health { get; set; } = 100f;
        public float Mana { get; set; } = 100f;
        public bool IsAlive { get; set; } = true;
        public DateTime LastDeathTime { get; set; } = DateTime.MinValue;
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
        public System.Numerics.Vector2 Position { get; set; } = new(0, 0);
        public Hero? Hero { get; set; }

        public void InitializeHero(int heroId)
        {
            Hero = HeroFactory.CreateHero(heroId);
            Health = Hero.BaseHealth;
            Mana = Hero.BaseMana;
        }
    }

    public class GameMatch
    {
        public string MatchId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public bool IsActive { get; set; } = false;
    public MatchState MatchState { get; set; } = MatchState.Waiting;

    public Dictionary<string, Player> Players { get; set; } = new();
    }

    public class GameStateManager(ILogger<GameStateManager> logger)
    {
        private readonly Dictionary<string, GameMatch> _activeMatches = new Dictionary<string, GameMatch>();
        private readonly ILogger<GameStateManager> _logger = logger;

        public GameMatch CreateMatch()
        {
            var matchId = Guid.NewGuid().ToString();
            var match = new GameMatch
            {
                MatchId = matchId,
                StartTime = DateTime.UtcNow,
                IsActive = false
            };

            _activeMatches[matchId] = match;
            _logger.LogInformation($"Match created: {matchId}");
            return match;
        }

        public GameMatch? GetMatch(string matchId)
        {
            if (_activeMatches.TryGetValue(matchId, out var match))
            {
                return match;
            }
            return null;
        }

        public bool AddPlayerToMatch(string matchId, Player player)
        {
            if (_activeMatches.TryGetValue(matchId, out var match))
            {
                if (!string.IsNullOrEmpty(player.ConnectionId))
                {
                    AssignPlayerTeam(match, player);  // Add this line to assign team
                    match.Players[player.ConnectionId] = player;
                    _logger.LogInformation($"Player {player.Username} added to match {matchId} on team {player.TeamId}");
                    return true;
                }
                _logger.LogWarning("Attempted to add player with null or empty ConnectionId to match.");
                return false;
            }
            return false;
        }

        private void AssignPlayerTeam(GameMatch match, Player player)
        {
            int team1Count = match.Players.Values.Count(p => p.TeamId == 1);
            int team2Count = match.Players.Values.Count(p => p.TeamId == 2);

            // Always assign team based on current counts
            player.TeamId = team1Count <= team2Count ? 1 : 2;
            
            _logger.LogInformation($"Assigned player {player.Username} to team {player.TeamId}");
        }

        public bool RemovePlayerFromMatch(string matchId, string connectionId)
        {
            if (_activeMatches.TryGetValue(matchId, out var match))
            {
                if (match.Players.Remove(connectionId))
                {
                    _logger.LogInformation($"Player {connectionId} removed from match {matchId}");

                    if (match.Players.Count == 0)
                    {
                        _ = _activeMatches.Remove(matchId);
                        _logger.LogInformation($"Match {matchId} removed as it has no players");
                    }
                    
                    return true;
                }
            }
            return false;
        }

        public List<GameMatch> GetAvailableMatches()
        {
            var result = new List<GameMatch>();
            foreach (var match in _activeMatches.Values)
            {
                if (match.Players.Count < 10) 
                {
                    result.Add(match);
                }
            }
            return result;
        }

        internal void CleanupAllMatches()
        {
            throw new NotImplementedException();
        }

        internal IEnumerable<object> GetAllMatches()
        {
            return _activeMatches.Values;
        }
    }

    public class MobaHub(GameStateManager gameState, ILogger<MobaHub> logger) : Hub
    {
        private readonly GameStateManager _gameState = gameState;
        private readonly ILogger<MobaHub> _logger = logger;

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");

            foreach (var match in _gameState.GetAvailableMatches())
            {
                if (match.Players.ContainsKey(Context.ConnectionId))
                {
                    if (!string.IsNullOrEmpty(match.MatchId))
                    {
                        _ = _gameState.RemovePlayerFromMatch(match.MatchId, Context.ConnectionId);
                        await Clients.Group(match.MatchId).SendAsync("PlayerLeft", Context.ConnectionId);
                    }
                    break;
                }
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        public async Task RegisterPlayer(string username, int heroId)
        {
            var player = new Player
            {
                ConnectionId = Context.ConnectionId,
                Username = username,
                HeroId = heroId,
                Health = 100,
                Mana = 100,
                IsAlive = true,
                Position = new Vector2(0, 0),
                LastUpdateTime = DateTime.UtcNow
            };
            player.InitializeHero(heroId);

            await Clients.Caller.SendAsync("RegistrationConfirmed", player);
            _logger.LogInformation($"Player registered: {username}, Hero ID: {heroId}");
        }

        public async Task CreateAndJoinMatch()
        {
            var match = _gameState.CreateMatch();

            if (!string.IsNullOrEmpty(match.MatchId))
            {
                await JoinMatch(match.MatchId);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Failed to create match: MatchId is null");
            }
        }

        public async Task JoinMatch(string matchId)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null)
            {
                await Clients.Caller.SendAsync("Error", "Match not found");
                return;
            }

            if (match.Players.Count >= 10)
            {
                await Clients.Caller.SendAsync("Error", "Match is full");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, matchId);
            
            if (!match.Players.TryGetValue(Context.ConnectionId, out var player))
            {

                player = new Player
                {
                    ConnectionId = Context.ConnectionId,
                    Username = $"Player_{Context.ConnectionId.Substring(0, 5)}",
                    HeroId = 1,
                    Health = 100,
                    Mana = 100,
                    IsAlive = true,
                    Position = new Vector2(0, 0),
                    LastUpdateTime = DateTime.UtcNow
                };
            }

            _ = _gameState.AddPlayerToMatch(matchId, player);
            
            await Clients.Group(matchId).SendAsync("PlayerJoined", player);

            await Clients.Caller.SendAsync("MatchState", match);
            
            _logger.LogInformation($"Player {player.Username} joined match {matchId}");
        }

        public async Task GetAvailableMatches()
        {
            var matches = _gameState.GetAvailableMatches();
            await Clients.Caller.SendAsync("AvailableMatches", matches);
        }

        public async Task MovePlayer(string matchId, float x, float y)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null || !match.Players.TryGetValue(Context.ConnectionId, out var player))
            {
                return;
            }

            player.Position = new Vector2(x, y);
            player.LastUpdateTime = DateTime.UtcNow;

            await Clients.Group(matchId).SendAsync("PlayerMoved", Context.ConnectionId, x, y);
        }

        public async Task UseAbility(string matchId, int abilityId, float targetX, float targetY)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null || !match.Players.TryGetValue(Context.ConnectionId, out var player))
            {
                return;
            }

            await Clients.Group(matchId).SendAsync("AbilityUsed", Context.ConnectionId, abilityId, targetX, targetY);
            _logger.LogInformation($"Player {player.Username} used ability {abilityId}");
        }

        private readonly Dictionary<string, Timer> _respawnTimers = new Dictionary<string, Timer>();

        public async Task AttackPlayer(string matchId, string targetPlayerId)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null || 
                !match.Players.TryGetValue(Context.ConnectionId, out var attacker) ||
                !match.Players.TryGetValue(targetPlayerId, out var target))
            {
                return;
            }

            // Проверяем, что цель жива и из другой команды
            if (!target.IsAlive || target.TeamId == attacker.TeamId)
            {
                return;
            }

            target.Health = Math.Max(0, target.Health - 10);
            
            if (target.Health <= 0)
            {
                target.IsAlive = false;
                target.LastDeathTime = DateTime.UtcNow;
                
                // Устанавливаем позицию на спавн
                target.Position = GetSpawnPosition(target.TeamId);
                
                await Clients.Group(matchId).SendAsync("PlayerDied", targetPlayerId);
                _logger.LogInformation($"Player {target.Username} died");

                // Запускаем таймер возрождения
                StartRespawnTimer(matchId, targetPlayerId, target);
            }

            await Clients.Group(matchId).SendAsync("PlayerAttacked", Context.ConnectionId, targetPlayerId, target.Health);
            _logger.LogInformation($"Player {attacker.Username} attacked {target.Username}");
        }

        private void StartRespawnTimer(string matchId, string playerId, Player player)
        {
            // Отменяем предыдущий таймер, если он есть
            if (_respawnTimers.ContainsKey(playerId))
            {
                _respawnTimers[playerId].Dispose();
                _respawnTimers.Remove(playerId);
            }

            // Создаем новый таймер на 20 секунд
            var timer = new Timer(async _ =>
            {
                await RespawnPlayer(matchId, playerId);
                
                if (_respawnTimers.ContainsKey(playerId))
                {
                    _respawnTimers[playerId].Dispose();
                    _respawnTimers.Remove(playerId);
                }
                
            }, null, TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(-1));

            _respawnTimers[playerId] = timer;
        }

        private async Task RespawnPlayer(string matchId, string playerId)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null || !match.Players.TryGetValue(playerId, out var player))
            {
                return;
            }

            // Восстанавливаем игрока
            player.IsAlive = true;
            player.Health = 100f;
            player.Mana = 100f;
            player.Position = GetSpawnPosition(player.TeamId);

            await Clients.Group(matchId).SendAsync("PlayerRespawned", player);
            _logger.LogInformation($"Player {player.Username} respawned");
        }

        private System.Numerics.Vector2 GetSpawnPosition(int teamId)
        {
            return teamId switch
            {
                1 => new System.Numerics.Vector2(-7f, 0f),
                2 => new System.Numerics.Vector2(7f, 0f),
                _ => new System.Numerics.Vector2(0f, 0f)
            };
        }

        public async Task RequestRespawn(string matchId)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null || !match.Players.TryGetValue(Context.ConnectionId, out var player))
            {
                return;
            }

            // Проверяем, прошло ли достаточно времени с момента смерти
            var timeSinceDeath = DateTime.UtcNow - player.LastDeathTime;
            if (timeSinceDeath.TotalSeconds >= 20 && !player.IsAlive)
            {
                await RespawnPlayer(matchId, Context.ConnectionId);
            }
        }
        public async Task SendMessage(string matchId, string message)
        {
            if (_gameState.GetMatch(matchId) == null)
            {
                return;
            }

            string username = "Unknown";
            var match = _gameState.GetMatch(matchId);
            if (match != null && match.Players.TryGetValue(Context.ConnectionId, out var player))
            {
                username = player.Username ?? "Unknown";
            }

            await Clients.Group(matchId).SendAsync("ChatMessage", username, message);
            _logger.LogInformation($"Chat message in match {matchId} from {username}: {message}");
        }
    }
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            _ = services.AddSignalR();
            _ = services.AddSingleton<GameStateManager>();
            _ = services.AddLogging(logging =>
            {
                _ = logging.AddConsole();
                _ = logging.SetMinimumLevel(LogLevel.Information);
            });
            _ = services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    _ = builder
                         .SetIsOriginAllowed(origin => true) // разрешить все origin
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            _ = app.UseCors();

            _ = app.UseRouting();

            _ = app.UseEndpoints(endpoints =>
            {
                _ = endpoints.MapHub<MobaHub>("/mobahub");
            });
        }
    }

}