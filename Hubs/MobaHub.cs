using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MobaSignalRServer.Models;
using MobaSignalRServer.Services;
using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using MobaSignalRServer.Models.Heroes;

namespace MobaSignalRServer
{
    public class MobaGameHub : Hub
    {
        private const int MAX_PLAYERS_PER_MATCH = 4;
        private const int MAX_PLAYERS_PER_TEAM = 2; 

        private readonly GameStateManager _gameState;
        private readonly ILogger<MobaGameHub> _logger;
        private readonly IServiceProvider _serviceProvider;

        private static readonly ConcurrentDictionary<string, Player> _registeredPlayers = new();

        public MobaGameHub(GameStateManager gameState, ILogger<MobaGameHub> logger, IServiceProvider serviceProvider)
        {
            _gameState = gameState;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");

            _registeredPlayers.TryRemove(Context.ConnectionId, out _);

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
                IsAlive = true,
                Position = new Vector2(0, 0),
                LastUpdateTime = DateTime.UtcNow
            };
            
            player.InitializeHero(heroId);

            _registeredPlayers[Context.ConnectionId] = player;

            var playerData = new PlayerDataDto(player);

            _logger.LogInformation($"RegistrationConfirmed: ConnId={playerData.connectionId}, Username={playerData.username}, HeroId={playerData.heroId}, TeamId={playerData.teamId}");
            await Clients.Caller.SendAsync("RegistrationConfirmed", playerData);
            _logger.LogInformation($"Player registered: {username}, Hero ID: {heroId}");
        }

        #endregion

        #region Match Management

        public async Task CreateAndJoinMatch()
        {
            var match = _gameState.CreateMatch();

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

            if (match.Players.Count >= MAX_PLAYERS_PER_MATCH)
            {
                await Clients.Caller.SendAsync("Error", "Match is full");
                return;
            }

            int team1Count = match.Players.Values.Count(p => p.TeamId == 1);
            int team2Count = match.Players.Values.Count(p => p.TeamId == 2);
            
            if (team1Count >= MAX_PLAYERS_PER_TEAM && team2Count >= MAX_PLAYERS_PER_TEAM)
            {
                await Clients.Caller.SendAsync("Error", "Both teams are full");
                return;
            }

            if (!_registeredPlayers.TryGetValue(Context.ConnectionId, out var player))
            {
                _logger.LogWarning($"❌ Player not found in _registeredPlayers for ConnId={Context.ConnectionId}");
                await Clients.Caller.SendAsync("Error", "You must register before joining a match");
                return;
            }

            bool added = _gameState.AddPlayerToMatch(matchId, player);
            if (!added)
            {
                _logger.LogWarning($"❌ Failed to add player {player.Username} to match {matchId}");
                await Clients.Caller.SendAsync("Error", "Failed to join match");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, matchId);

            _logger.LogInformation($"✅ Assigned player {player.Username} to team {player.TeamId}");
            _logger.LogInformation($"📥 Player {player.Username} added to match {matchId} (ConnId: {Context.ConnectionId})");

            var playerJoinedData = new PlayerDataDto(player);

            await Clients.Group(matchId).SendAsync("PlayerJoined", playerJoinedData);
            _logger.LogInformation($"PlayerJoined sent: ConnId={playerJoinedData.connectionId}, Username={playerJoinedData.username}, HeroId={playerJoinedData.heroId}, TeamId={playerJoinedData.teamId}");

            var updatedMatch = _gameState.GetMatch(matchId);

            if (updatedMatch == null)
            {
                _logger.LogWarning($"Updated match not found for matchId: {matchId}");
                await Clients.Caller.SendAsync("Error", "Match not found after joining.");
                return;
            }

            var playersData = updatedMatch.Players.Values.Select(p => new PlayerDataDto(p)).ToList();

            var matchData = new
            {
                matchId = updatedMatch.MatchId,
                players = playersData,
                startTime = updatedMatch.StartTime.ToUniversalTime().ToString("o"),
                isActive = updatedMatch.IsActive
            };

            await Clients.Caller.SendAsync("MatchState", matchData);

            _logger.LogInformation($"✅ Player {player.Username} joined match {matchId} with HeroId: {player.HeroId}, TeamId: {player.TeamId}");
            await Task.Delay(100);
            await CheckAndStartGame(matchId);
        }

        public async Task GetAvailableMatches()
        {
            var matches = _gameState.GetAvailableMatches();

            var matchesData = matches.Select(m => new
            {
                matchId = m.MatchId,
                players = m.Players.Values.Select(p => new PlayerDataDto(p)).ToList(),
                startTime = m.StartTime.ToUniversalTime().ToString("o"),
                isActive = m.IsActive
            }).ToList();

            await Clients.Caller.SendAsync("AvailableMatches", matchesData);
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

            await Clients.Group(matchId).SendAsync("PlayerMoved", Context.ConnectionId, x, y);
        }

        public async Task UseAbility(string matchId, int abilityId, float targetX, float targetY)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null || !match.Players.TryGetValue(Context.ConnectionId, out var player))
            {
                return;
            }

            var targetPosition = new Vector2(targetX, targetY);
            AbilityResult? result = null;

            if (player.Hero != null)
            {
                switch (abilityId)
                {
                    case 1:
                        result = player.Hero.Ability1(targetPosition);
                        break;
                    case 2:
                        result = player.Hero.Ability2(targetPosition);
                        break;
                    case 3:
                        result = player.Hero.Ability3(targetPosition);
                        break;
                }
            }
            else
            {
                _logger.LogWarning($"Player {player.Username} has no hero assigned.");
                await Clients.Caller.SendAsync("Error", "No hero assigned to player.");
                return;
            }

            if (result != null)
            {
                await Clients.Group(matchId).SendAsync("AbilityUsed", new
                {
                    playerId = Context.ConnectionId,
                    abilityId = abilityId,
                    targetX = targetX,
                    targetY = targetY,
                    damage = result.Damage,
                    range = result.Range,
                    areaOfEffect = result.AreaOfEffect,
                    buffDuration = result.BuffDuration,
                    type = result.Type
                });
                
                _logger.LogInformation($"Player {player.Username} used ability {abilityId} of type {result.Type}");
            }
        }

        public async Task CheckAndStartGame(string matchId)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null) return;

            int team1Count = match.Players.Values.Count(p => p.TeamId == 1);
            int team2Count = match.Players.Values.Count(p => p.TeamId == 2);
            _logger.LogInformation($"🧪 GameStartCheck → Team1: {team1Count}, Team2: {team2Count}, IsActive: {match.IsActive}");
            if (team1Count >= 2 && team2Count >= 2 && !match.IsActive)
            {
                _logger.LogInformation($"🎮 2 players found on each team. Game starts in 5 seconds.");


                await Clients.Group(matchId).SendAsync("GameStarting", 10);

                await Task.Delay(10000);

                match.IsActive = true;

                await Clients.Group(matchId).SendAsync("GameStarted");

                _logger.LogInformation($"🚀 Playing in a match {matchId} started.");
            }
        }

        public async Task RespawnPlayer(string matchId, string connId)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null) return;

            if (match.Players.TryGetValue(connId, out var player))
            {
                player.IsAlive = true;
                player.Health = 100;
                player.Mana = 100;

                await Clients.Client(connId).SendAsync("PlayerRespawned", new PlayerDataDto(player));
                await Clients.Group(matchId).SendAsync("ChatMessage", player.Username, "🌀 Respawned!");
            }
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

            if (attacker.ConnectionId == target.ConnectionId)
            {
                _logger.LogWarning($"⚠ Игрок {attacker.Username} попытался атаковать сам себя.");
                return;
            }

            if (attacker.TeamId == target.TeamId)
            {
                _logger.LogWarning($"⚠ Игрок {attacker.Username} попытался атаковать союзника {target.Username}");
                return;
            }

            float damage = attacker.Hero?.BaseAttackDamage ?? 10f;
            target.Health = Math.Max(0, target.Health - damage);

            if (target.Health <= 0 && target.IsAlive)
            {
                target.IsAlive = false;
                target.LastDeathTime = DateTime.UtcNow;

                await Clients.Group(matchId).SendAsync("PlayerDied", targetPlayerId);
                _logger.LogInformation($"☠ Player {target.Username} died");

                _ = ScheduleRespawn(target.ConnectionId, matchId);
            }

            await Clients.Group(matchId).SendAsync("PlayerAttacked", Context.ConnectionId, targetPlayerId, target.Health);
            _logger.LogInformation($"⚔ Player {attacker.Username} attacked {target.Username} for {damage} damage");

        }

            [HubMethodName("SendAttackAnimation")]
            public async Task SendAttackAnimation(string matchId, string targetConnectionId, int heroId, 
                float attackerX, float attackerY, float targetX, float targetY)
            {
                try
                {
                     var match = _gameState.GetMatch(matchId);
                    if (match == null)
                    {
                        await Clients.Caller.SendAsync("Error", "Матч не найден");
                        return;
                    }

                    var attackerPair = match.Players.FirstOrDefault(p => p.Value.ConnectionId == Context.ConnectionId);
                    var attacker = attackerPair.Value;

                    if (attacker == null)
                    {
                        await Clients.Caller.SendAsync("Error", "Игрок не найден в матче");
                        return;
                    }

                    var targetPair = match.Players.FirstOrDefault(p => p.Value.ConnectionId == targetConnectionId);
                    var target = targetPair.Value;

                    if (target == null)
                    {
                        await Clients.Caller.SendAsync("Error", "Цель не найдена");
                        return;
                    }

                    if (!attacker.IsAlive)
                    {
                        await Clients.Caller.SendAsync("Error", "Нельзя атаковать будучи мертвым");
                        return;
                    }

                    if (!target.IsAlive)
                    {
                        await Clients.Caller.SendAsync("Error", "Нельзя атаковать мертвую цель");
                        return;
                    }

                    await Clients.Group(matchId).SendAsync("AttackAnimation", 
                        attacker.ConnectionId, targetConnectionId, heroId, 
                        attackerX, attackerY, targetX, targetY);

                    Console.WriteLine($"🎬 Анимация атаки отправлена: {attacker.Username} -> {target.Username} (Hero: {heroId})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка в SendAttackAnimation: {ex.Message}");
                    await Clients.Caller.SendAsync("Error", "Ошибка при обработке анимации атаки");
                }
            }
        private async Task ScheduleRespawn(string playerConnectionId, string matchId)
        {
            try
            {
                var respawnService = _serviceProvider.GetService(typeof(RespawnService)) as RespawnService;
                if (respawnService != null)
                {
                    await respawnService.ScheduleRespawn(playerConnectionId, matchId, TimeSpan.FromSeconds(20));
                    _logger.LogInformation($"⏰ Scheduled respawn for player {playerConnectionId} in match {matchId}");
                }
                else
                {
                    _logger.LogError("❌ RespawnService not found in DI container");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error scheduling respawn: {ex.Message}");
            }
        }

        #endregion

        #region Chat System

        public async Task SendMessage(string matchId, string message)
        {
            var match = _gameState.GetMatch(matchId);
            if (match == null)
            {
                return;
            }

            string username = "Unknown";
            if (match.Players.TryGetValue(Context.ConnectionId, out var player))
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

    public class PlayerDataDto
    {
        public string connectionId { get; }
        public string username { get; }
        public int heroId { get; }
        public int teamId { get; }
        public float health { get; }
        public float mana { get; }
        public bool isAlive { get; }
        public PositionDto position { get; }

        public float baseHealth { get; }
        public float baseMana { get; }
        public float baseAttackDamage { get; }
        public float baseAttackRange { get; }
        public float baseMovementSpeed { get; }


        public PlayerDataDto(Player player)
        {
            connectionId = player.ConnectionId;
            username = player.Username;
            heroId = player.HeroId;
            teamId = player.TeamId;
            health = player.Health;
            mana = player.Mana;
            isAlive = player.IsAlive;
            position = new PositionDto(player.Position);

            baseHealth = player.Hero?.BaseHealth ?? 100;
            baseMana = player.Hero?.BaseMana ?? 50;
            baseAttackDamage = player.Hero?.BaseAttackDamage ?? 10;
            baseAttackRange = player.Hero?.BaseAttackRange ?? 1.5f;
            baseMovementSpeed = player.Hero?.BaseMovementSpeed ?? 5f;
        }
    }

    public class PositionDto
    {
        public float x { get; }
        public float y { get; }

        public PositionDto(Vector2 pos)
        {
            x = pos.X;
            y = pos.Y;
        }
    }
}

namespace MobaSignalRServer.Models.Heroes
{
        public class AbilityResult
        {
            public float Damage { get; set; }
            public float Healing { get; set; }
            public float BuffDuration { get; set; }
            public Vector2 TargetPosition { get; set; }
            public float Range { get; set; }
            public float AreaOfEffect { get; set; }
            public AbilityType Type { get; set; }
        }
    
        public enum AbilityType
        {
            Damage,
            Buff,
            Movement,
            AreaEffect
        }
    public abstract class Hero
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public float BaseHealth { get; set; }
        public float BaseMana { get; set; }
        public float BaseAttackDamage { get; set; }
        public float BaseAttackRange { get; set; }
        public float BaseMovementSpeed { get; set; }

        public abstract AbilityResult Ability1(Vector2 targetPosition);
        public abstract AbilityResult Ability2(Vector2 targetPosition);
        public abstract AbilityResult Ability3(Vector2 targetPosition);
    }

    public class Warrior : Hero
    {
        public Warrior()
        {
            Id = 1;
            Name = "Warrior";
            BaseHealth = 150;
            BaseMana = 50;
            BaseAttackDamage = 15;
            BaseAttackRange = 12;
            BaseMovementSpeed = 5;
        }

        public override AbilityResult Ability1(Vector2 targetPosition)
        {
            return new AbilityResult
            {
                Damage = 20,
                Range = 20f,
                Type = AbilityType.Movement,
                TargetPosition = targetPosition,
                BuffDuration = 2f
            };
        }

        public override AbilityResult Ability2(Vector2 targetPosition) 
        {
            return new AbilityResult
            {
                Damage = 35,
                AreaOfEffect = 5f,
                Type = AbilityType.AreaEffect,
                TargetPosition = targetPosition,
                BuffDuration = 3f 
            };
        }

        public override AbilityResult Ability3(Vector2 targetPosition)
        {
            return new AbilityResult
            {
                BuffDuration = 5f,
                Type = AbilityType.Buff,
                TargetPosition = targetPosition,
                Range = 0f 
            };
        }
    }

    public class Mage : Hero
    {
        public Mage()
        {
            Id = 2;
            Name = "Mage";
            BaseHealth = 80;
            BaseMana = 150;
            BaseAttackDamage = 8;
            BaseAttackRange = 15;
            BaseMovementSpeed = 4;
        }

        public override AbilityResult Ability1(Vector2 targetPosition) 
        {
            return new AbilityResult
            {
                Damage = 45,
                Range = 30f,
                Type = AbilityType.Damage,
                TargetPosition = targetPosition,
                AreaOfEffect = 2f
            };
        }

        public override AbilityResult Ability2(Vector2 targetPosition)
        {
            return new AbilityResult
            {
                Damage = 30,
                Range = 18f,
                Type = AbilityType.AreaEffect,
                TargetPosition = targetPosition,
                AreaOfEffect = 6f,
                BuffDuration = 2f 
            };
        }

        public override AbilityResult Ability3(Vector2 targetPosition) 
        {
            return new AbilityResult
            {
                Range = 12f,
                Type = AbilityType.Movement,
                TargetPosition = targetPosition
            };
        }
    }

    public class Archer : Hero
    {
        public Archer()
        {
            Id = 3;
            Name = "Archer";
            BaseHealth = 100;
            BaseMana = 80;
            BaseAttackDamage = 12;
            BaseAttackRange = 17;
            BaseMovementSpeed = 6;
        }

        public override AbilityResult Ability1(Vector2 targetPosition)
        {
            return new AbilityResult
            {
                Damage = 50,
                Range = 30f,
                Type = AbilityType.Damage,
                TargetPosition = targetPosition
            };
        }

        public override AbilityResult Ability2(Vector2 targetPosition)
        {
            return new AbilityResult
            {
                Damage = 25,
                Range = 25f,
                Type = AbilityType.AreaEffect,
                TargetPosition = targetPosition,
                AreaOfEffect = 8f,
                BuffDuration = 4f 
            };
        }

        public override AbilityResult Ability3(Vector2 targetPosition)
        {
            return new AbilityResult
            {
                Range = 16f,
                Type = AbilityType.Movement,
                TargetPosition = targetPosition,
                BuffDuration = 1f
            };
        }
    }

    public static class HeroFactory
    {
        public static Hero CreateHero(int heroId)
        {
            return heroId switch
            {
                1 => new Warrior(),
                2 => new Mage(),
                3 => new Archer(),
                _ => throw new ArgumentException("Invalid hero ID")
            };
        }
    }
}