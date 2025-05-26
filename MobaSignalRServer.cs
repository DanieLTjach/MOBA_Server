using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using Microsoft.Extensions.Logging;


namespace MobaSignalRServer
{
    public class Player
    {
        public string? ConnectionId { get; set; }
        public int TeamId { get; set; } 
        public DateTime LastDeathTime { get; set; }
        public string? Username { get; set; }
        public int HeroId { get; set; }
        public Vector2 Position { get; set; }
        public float Health { get; set; }
        public float Mana { get; set; }
        public bool IsAlive { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }

    public class GameMatch
    {
        public string? MatchId { get; set; }
        public Dictionary<string, Player> Players { get; set; } = new Dictionary<string, Player>();
        public DateTime StartTime { get; set; }
        public bool IsActive { get; set; }
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
                IsActive = true
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

        public async Task AttackPlayer(string matchId, string targetPlayerId)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null || 
                !match.Players.TryGetValue(Context.ConnectionId, out var attacker) ||
                !match.Players.TryGetValue(targetPlayerId, out var target))
            {
                return;
            }

            target.Health = Math.Max(0, target.Health - 10);
            
            if (target.Health <= 0)
            {
                target.IsAlive = false;
                await Clients.Group(matchId).SendAsync("PlayerDied", targetPlayerId);
                _logger.LogInformation($"Player {target.Username} died");

            }

            await Clients.Group(matchId).SendAsync("PlayerAttacked", Context.ConnectionId, targetPlayerId, target.Health);
            _logger.LogInformation($"Player {attacker.Username} attacked {target.Username}");
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
                    _ = builder.WithOrigins("0.0.0.0") 
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