using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MobaSignalRServer.Models;
using MobaSignalRServer.Services;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace MobaSignalRServer
{
    public class MobaGameHub(GameStateManager gameState, ILogger<MobaGameHub> logger) : Hub
    {
        private readonly GameStateManager _gameState = gameState;
        private readonly ILogger<MobaGameHub> _logger = logger;

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
            
            // Find and remove player from any match they're in
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

        #region Authentication and Player Setup

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

            // Store player data
            await Clients.Caller.SendAsync("RegistrationConfirmed", player);
            _logger.LogInformation($"Player registered: {username}, Hero ID: {heroId}");
        }

        #endregion

        #region Match Management

        public async Task CreateAndJoinMatch()
        {
            var match = _gameState.CreateMatch();
            
            // Add player to match if matchId is not null
            if (!string.IsNullOrEmpty(match?.MatchId))
            {
                await JoinMatch(match.MatchId);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Failed to create match");
                _logger.LogError("Failed to create match or matchId is null.");
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

            // Add player to the match group
            await Groups.AddToGroupAsync(Context.ConnectionId, matchId);
            
            // Get player data from connection
            if (!match.Players.TryGetValue(Context.ConnectionId, out var player))
            {
                // If not found, create a default player
                player = new Player
                {
                    ConnectionId = Context.ConnectionId,
                    Username = $"Player_{Context.ConnectionId.Substring(0, 5)}",
                    HeroId = 1, // Default hero
                    Health = 100,
                    Mana = 100,
                    IsAlive = true,
                    Position = new Vector2(0, 0),
                    LastUpdateTime = DateTime.UtcNow
                };
            }

            // Add player to match
            _ = _gameState.AddPlayerToMatch(matchId, player);
            
            // Notify others of new player
            await Clients.Group(matchId).SendAsync("PlayerJoined", player);
            
            // Send current match state to the new player
            await Clients.Caller.SendAsync("MatchState", match);
            
            _logger.LogInformation($"Player {player.Username} joined match {matchId}");
        }

        public async Task GetAvailableMatches()
        {
            var matches = _gameState.GetAvailableMatches();
            await Clients.Caller.SendAsync("AvailableMatches", matches);
        }

        public async Task LeaveMatch(string matchId)
        {
            if (_gameState.RemovePlayerFromMatch(matchId, Context.ConnectionId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, matchId);
                await Clients.Group(matchId).SendAsync("PlayerLeft", Context.ConnectionId);
                _logger.LogInformation($"Player {Context.ConnectionId} left match {matchId}");
            }
        }

        #endregion

        #region Game Actions

        public async Task MovePlayer(string matchId, float x, float y)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null || !match.Players.TryGetValue(Context.ConnectionId, out var player))
            {
                return;
            }

            player.Position = new Vector2(x, y);
            player.LastUpdateTime = DateTime.UtcNow;
            
            // Broadcast position update to all players in the match
            await Clients.Group(matchId).SendAsync("PlayerMoved", Context.ConnectionId, x, y);
        }

        public async Task UseAbility(string matchId, int abilityId, float targetX, float targetY)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null || !match.Players.TryGetValue(Context.ConnectionId, out var player))
            {
                return;
            }

            // Implement ability logic here
            // For simplicity, just broadcast the ability use
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

            // Simple attack logic - reduce health by 10
            target.Health = Math.Max(0, target.Health - 10);
            
            if (target.Health <= 0)
            {
                target.IsAlive = false;
                await Clients.Group(matchId).SendAsync("PlayerDied", targetPlayerId);
                _logger.LogInformation($"Player {target.Username} died");
                
                // Respawn logic could be added here
                // Implement respawn timer and logic
            }

            await Clients.Group(matchId).SendAsync("PlayerAttacked", Context.ConnectionId, targetPlayerId, target.Health);
            _logger.LogInformation($"Player {attacker.Username} attacked {target.Username}");
        }

        #endregion

        #region Chat System

        public async Task SendMessage(string matchId, string message)
        {
            if (_gameState.GetMatch(matchId) == null)
            {
                return;
            }

            // Get the player's username
            string username = "Unknown";
            var match = _gameState.GetMatch(matchId);
            if (match != null && match.Players.TryGetValue(Context.ConnectionId, out var player))
            {
                username = player.Username ?? "Unknown";
            }

            await Clients.Group(matchId).SendAsync("ChatMessage", username, message);
            _logger.LogInformation($"Chat message in match {matchId} from {username}: {message}");
        }

        public async Task SendTeamMessage(string matchId, string message, int teamId)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null || !match.Players.TryGetValue(Context.ConnectionId, out var sender))
            {
                return;
            }

            // Get all players on the same team and send message only to them
            foreach (var player in match.Players.Values)
            {
                if (player.TeamId == teamId && !string.IsNullOrEmpty(player.ConnectionId))
                {
                    await Clients.Client(player.ConnectionId).SendAsync("TeamChatMessage", sender.Username, message);
                }
            }

            _logger.LogInformation($"Team chat message in match {matchId} from {sender.Username} to team {teamId}");
        }

        #endregion
    }
}