using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MobaSignalRServer.Services;

namespace MobaSignalRServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container
            _ = builder.Services.AddSignalR();
            _ = builder.Services.AddSingleton<GameStateManager>();

            // Add CORS
            _ = builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    _ = builder.WithOrigins("http://localhost:5500", "http://127.0.0.1:5500") // Add your client origins
                           .AllowAnyHeader()
                           .AllowAnyMethod()
                           .AllowCredentials();
                });
            });

            // Configure logging
            _ = builder.Logging.AddConsole();
            _ = builder.Logging.SetMinimumLevel(LogLevel.Information);

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                _ = app.UseDeveloperExceptionPage();
            }

            _ = app.UseRouting();
            _ = app.UseCors();

            // Map hubs
            _ = app.MapHub<MobaHub>("/mobahub");

            // Start the game server loop in the background
            var logger = app.Services.GetRequiredService<ILogger<GameServer>>();
            var gameStateManager = app.Services.GetRequiredService<GameStateManager>();
            var gameServer = new GameServer(app, gameStateManager, logger);
            gameServer.Start(); // This will call app.Run() and start the game loop
        }
    }
}