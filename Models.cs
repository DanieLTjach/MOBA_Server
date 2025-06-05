using System;
using System.Collections.Generic;
using System.Numerics;

namespace MobaSignalRServer.Models
{
    public class Player
    {
        public string ConnectionId { get; set; } = "";
        public string Username { get; set; } = "";
        public int HeroId { get; set; }
        public int TeamId { get; set; }

        public Vector2 Position { get; set; }
        public float Health { get; set; }
        public float Mana { get; set; }
        public bool IsAlive { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public DateTime LastDeathTime { get; set; }

        public int Level { get; set; } = 1;
        public int Experience { get; set; } = 0;
        public int Kills { get; set; } = 0;
        public int Deaths { get; set; } = 0;
        public int Assists { get; set; } = 0;
    }

    public class GameMatch
    {
        public string MatchId { get; set; } = "";
        
        public Dictionary<string, Player> Players { get; set; } = new();

        public DateTime StartTime { get; set; }
        public bool IsActive { get; set; }

        public int Team1Score { get; set; } = 0;
        public int Team2Score { get; set; } = 0;

        public Dictionary<int, GameObjectState> GameObjects { get; set; } = new();

        public MatchState State { get; set; } = MatchState.Waiting;
    }

    public class GameObjectState
    {
        public int Id { get; set; }
        public string? Type { get; set; }
        public int TeamId { get; set; } 
        public Vector2 Position { get; set; }
        public float Health { get; set; }
        public bool IsAlive { get; set; }
    }

    public enum MatchState
    {
        Waiting,
        InProgress,
        Finished
    }

    public class AbilityUse
    {
        public string? PlayerId { get; set; }
        public int AbilityId { get; set; }
        public Vector2 TargetPosition { get; set; }
        public string? TargetPlayerId { get; set; }
        public DateTime UseTime { get; set; }
    }

    public class ChatMessage
    {
        public string? PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public string? Message { get; set; }
        public int TeamId { get; set; } 
        public DateTime SentTime { get; set; }
    }
}