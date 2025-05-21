using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using MobaSignalRServer.Models;

namespace MobaSignalRServer.Services
{
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
                // Assign player to team
                AssignPlayerTeam(match, player);

                if (!string.IsNullOrEmpty(player.ConnectionId))
                {
                    match.Players[player.ConnectionId] = player;
                    _logger.LogInformation($"Player {player.Username} added to match {matchId} on team {player.TeamId}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Player {player.Username} has a null or empty ConnectionId and cannot be added to match {matchId}");
                    return false;
                }
            }
            return false;
        }

        private void AssignPlayerTeam(GameMatch match, Player player)
        {
            // Simple team balancing - count players on each team and assign to the team with fewer players
            int team1Count = match.Players.Values.Count(p => p.TeamId == 1);
            int team2Count = match.Players.Values.Count(p => p.TeamId == 2);
            
            player.TeamId = team1Count <= team2Count ? 1 : 2;
        }

        public bool RemovePlayerFromMatch(string matchId, string connectionId)
        {
            if (_activeMatches.TryGetValue(matchId, out var match))
            {
                if (match.Players.Remove(connectionId))
                {
                    _logger.LogInformation($"Player {connectionId} removed from match {matchId}");
                    
                    // If no players left, clean up the match
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
                if (match.Players.Count < 10) // Max 10 players for a typical MOBA
                {
                    result.Add(match);
                }
            }
            return result;
        }

        public List<GameMatch> GetAllMatches()
        {
            return _activeMatches.Values.ToList();
        }

        public void CleanupAllMatches()
        {
            foreach (var match in _activeMatches.Values)
            {
                match.IsActive = false;
            }
            
            _activeMatches.Clear();
            _logger.LogInformation("All matches have been cleaned up");
        }

        // Game state update methods
        public void UpdatePlayerHealth(string matchId, string playerId, float newHealth)
        {
            if (_activeMatches.TryGetValue(matchId, out var match) && 
                match.Players.TryGetValue(playerId, out var player))
            {
                player.Health = newHealth;
                
                // Check for player death
                if (player.Health <= 0 && player.IsAlive)
                {
                    player.IsAlive = false;
                    player.LastDeathTime = DateTime.UtcNow;
                    _logger.LogInformation($"Player {player.Username} died in match {matchId}");
                }
            }
        }

        public void UpdatePlayerMana(string matchId, string playerId, float newMana)
        {
            if (_activeMatches.TryGetValue(matchId, out var match) && 
                match.Players.TryGetValue(playerId, out var player))
            {
                player.Mana = newMana;
            }
        }

        public void UpdatePlayerPosition(string matchId, string playerId, float x, float y)
        {
            if (_activeMatches.TryGetValue(matchId, out var match) && 
                match.Players.TryGetValue(playerId, out var player))
            {
                player.Position = new System.Numerics.Vector2(x, y);
                player.LastUpdateTime = DateTime.UtcNow;
            }
        }
    }
}