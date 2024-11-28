using System;
using System.Collections.Concurrent; // For ConcurrentDictionary
using System.Collections.Generic;
using System.Threading.Tasks;
using Players; // Ensure this namespace is correctly referenced
using Characters; // Ensure this namespace is correctly referenced
using Microsoft.AspNetCore.SignalR; // Required for IHubContext
using SignalRChat.Hubs;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.WebEncoders.Testing;

namespace MMOServer
{
    public class Server
    {
        // Singleton instance
        private static readonly Server instance = new Server();
        private readonly IHubContext<GameHub> _hubContext;

        public Server(IHubContext<GameHub> hubContext)
        {
            _hubContext = hubContext;
        }

        // Private constructor to prevent instantiation
        private Server() {}

        public static Server Instance => instance;

        public int tickRate = 144;
        public bool is_gameStarted = false;

        public List<Player> team_blue { get; set; } = new List<Player>();

        public List<Player> team_red { get; set; } = new List<Player>(); 

        // Using ConcurrentDictionary for thread-safe operations
        private readonly ConcurrentDictionary<string, Player> players = new ConcurrentDictionary<string, Player>(); 
        
        // Asynchronous method to add a player
        public async Task AddPlayer(string username) 
        {
            // Check if the player already exists
            if (players.ContainsKey(username))
            {
                Console.WriteLine($"Player with ID {username} already exists.");
                return; // Exit the method if the player already exists
            }
            var randomTeam = new Random().Next(0, 2) == 0; 
            var randomCoordinates = new Random().Next(-10, 10);
            if (randomTeam == true)
            {
                if(team_blue.Count < 3){
                    team_blue.Add(new Player(
                        id: username,
                        coordinates: new Coordinates { X = 50 + randomCoordinates, Y = 550 + randomCoordinates },
                        characteristics: new Characteristics { Speed = 1, Attack = 1, Magic = 1, Defence = 1, Healthpoint = 1 },
                        team: true
                    ));
                }
                else if(team_red.Count < 3){
                    team_red.Add(new Player(
                        id: username,
                        coordinates: new Coordinates {  X = 550 + randomCoordinates, Y = 50 + randomCoordinates},
                        characteristics: new Characteristics { Speed = 1, Attack = 1, Magic = 1, Defence = 1, Healthpoint = 1 },
                        team: false
                    ));
                }
                else{
                    Console.WriteLine("Both teams are full");
                    return;
                }
            }
            else
            {
                if(team_red.Count < 3){
                    team_red.Add(new Player(
                        id: username,
                        coordinates: new Coordinates { X = 550 + randomCoordinates, Y = 50 + randomCoordinates },
                        characteristics: new Characteristics { Speed = 1, Attack = 1, Magic = 1, Defence = 1, Healthpoint = 1 },
                        team: false
                    ));
                }
                else if(team_blue.Count < 3){
                    team_blue.Add(new Player(
                        id: username,
                        coordinates: new Coordinates { X = 50 + randomCoordinates, Y = 550 + randomCoordinates  },
                        characteristics: new Characteristics { Speed = 1, Attack = 1, Magic = 1, Defence = 1, Healthpoint = 1 },
                        team: true
                    ));
                }
                else{
                    Console.WriteLine("Both teams are full");
                    return;
                }
            }

            Console.WriteLine("Players online:");
            Console.WriteLine("Team Blue:");
            foreach (var player in team_blue){
                Console.WriteLine(player.id);
            }
            Console.WriteLine("Team Red:");
            foreach (var player in team_red){
                Console.WriteLine(player.id);
            }
        }
        
         public async Task RemovePlayer(string user)
        {
            team_blue.RemoveAll(p => p.id == user);
            team_red.RemoveAll(p => p.id == user);
        }

        // Method to get the list of players
        public IEnumerable<Player> GetPlayerList()
        {
            return players.Values; // Return the list of Player objects
        }

        public async Task StartGame()
        {
            while (true)
            {
                await Task.Delay(1000 / tickRate);
            }
        }

        public async Task MovePlayer(string id, int TargetX, int TargetY)
        {
            if (!is_gameStarted)
            {
                Console.WriteLine("Game has not started yet.");
                return;
            }
            var player = team_blue.FirstOrDefault(p => p.id == id) ?? team_red.FirstOrDefault(p => p.id == id);
            if (player == null)
            {
                Console.WriteLine($"Player with ID {id} not found.");
                return;
            }
            else
            {
                bool inTarget = false;
                while(!inTarget){
                    if(player.coordinates.X < TargetX){
                        player.coordinates.X += player.characteristics.Speed;
                    }
                    else if(player.coordinates.X > TargetX){
                        player.coordinates.X -= player.characteristics.Speed;
                    }
                    if(player.coordinates.Y < TargetY){
                        player.coordinates.Y += player.characteristics.Speed;
                    }
                    else if(player.coordinates.Y > TargetY){
                        player.coordinates.Y -= player.characteristics.Speed;
                    }
                    if(player.coordinates.X == TargetX && player.coordinates.Y == TargetY){
                        inTarget = true;
                    }
                    //Console.WriteLine($"Player {player.id} moved to {player.coordinates.X}, {player.coordinates.Y}");
                    await NotifyPlayerMoved(player);
                    await Task.Delay(1000 / tickRate);
                }
            }
            
        }

        private async Task NotifyPlayerMoved(Player player)
        {
            // Notify all clients about the player's new position
            await _hubContext.Clients.All.SendAsync("PlayerMoved", player.id, player.coordinates.X, player.coordinates.Y);
        
        }
    }
}
