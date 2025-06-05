using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using MobaSignalRServer.Models;
using System.Numerics;

namespace MobaSignalRServer.Services
{
    public class GameStateManager
    {
        private readonly Dictionary<string, GameMatch> _activeMatches = new();
        private readonly ILogger<GameStateManager> _logger;

        public GameStateManager(ILogger<GameStateManager> logger)
        {
            _logger = logger;
        }

        public GameMatch CreateMatch()
        {
            var matchId = Guid.NewGuid().ToString();

            var match = new GameMatch
            {
                MatchId = matchId,
                StartTime = DateTime.UtcNow,
                IsActive = false,
                MatchState = MatchState.Waiting,
                Players = new Dictionary<string, Player>()
            };

            _activeMatches[matchId] = match;
            _logger.LogInformation($"Match created: {matchId}");
            return match;
        }

        public GameMatch? GetMatch(string matchId)
        {
            return _activeMatches.TryGetValue(matchId, out var match) ? match : null;
        }

        public bool AddPlayerToMatch(string matchId, Player player)
        {
            if (string.IsNullOrEmpty(player?.ConnectionId))
            {
                _logger.LogWarning("Player or ConnectionId is null. Cannot add to match.");
                return false;
            }

            if (_activeMatches.TryGetValue(matchId, out var match))
            {
                AssignPlayerTeam(match, player);

                match.Players[player.ConnectionId] = player;
                _logger.LogInformation($"Player {player.Username} added to match {matchId} on team {player.TeamId}");
                return true;
            }

            _logger.LogWarning($"Match {matchId} not found. Cannot add player.");
            return false;
        }

        private static void AssignPlayerTeam(GameMatch match, Player player)
        {
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

                    if (match.Players.Count == 0)
                    {
                        _activeMatches.Remove(matchId);
                        _logger.LogInformation($"Match {matchId} removed as it has no players left.");
                    }

                    return true;
                }
            }

            _logger.LogWarning($"Player {connectionId} not found in match {matchId}");
            return false;
        }

        public List<GameMatch> GetAvailableMatches()
        {
            return _activeMatches.Values.Where(m => m.Players.Count < 10).ToList();
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
            _logger.LogInformation("All matches have been cleaned up.");
        }

        public void UpdatePlayerHealth(string matchId, string playerId, float newHealth)
        {
            if (_activeMatches.TryGetValue(matchId, out var match) &&
                match.Players.TryGetValue(playerId, out var player))
            {
                player.Health = newHealth;

                if (player.Health <= 0 && player.IsAlive)
                {
                    player.IsAlive = false;
                    player.LastDeathTime = DateTime.UtcNow;
                    _logger.LogInformation($"Player {player.Username} died in match {matchId}");
                }
            }
            else
            {
                _logger.LogWarning($"Cannot update health. Player or match not found.");
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
                player.Position = new Vector2(x, y);
                player.LastUpdateTime = DateTime.UtcNow;
            }
        }
    }
}
