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
                    _ = builder.WithOrigins("0.0.0.0") 
                           .AllowAnyHeader()
                           .AllowAnyMethod()
                           .AllowCredentials();
                });
            });

         
            _ = builder.Logging.AddConsole();
            _ = builder.Logging.SetMinimumLevel(LogLevel.Information);

            var app = builder.Build();

         
            if (app.Environment.IsDevelopment())
            {
                _ = app.UseDeveloperExceptionPage();
            }

            _ = app.UseRouting();
            _ = app.UseCors();

          
            _ = app.MapHub<MobaHub>("/mobahub");

           
            var logger = app.Services.GetRequiredService<ILogger<GameServer>>();
            var gameStateManager = app.Services.GetRequiredService<GameStateManager>();
            var gameServer = new GameServer(app, gameStateManager, logger);
            gameServer.Start(); 
        }
    }
}