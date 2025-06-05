using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MobaSignalRServer.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace MobaSignalRServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ListenAnyIP(5201, options =>
                {
                    options.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
                });
            });

            _ = builder.Services.AddControllers();
            _ = builder.Services.AddSignalR()
                .AddNewtonsoftJsonProtocol();

            _ = builder.Services.AddSingleton<GameStateManager>();
            
            _ = builder.Services.AddSingleton<RespawnService>();
            _ = builder.Services.AddHostedService<RespawnService>(provider => provider.GetService<RespawnService>()!);

            _ = builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    _ = builder
                            .SetIsOriginAllowed(origin => true)
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

            _ = app.UseStaticFiles();
            _ = app.UseRouting();
            _ = app.UseCors();

            _ = app.MapControllers();
            _ = app.MapHub<MobaGameHub>("/mobahub");

            var logger = app.Services.GetRequiredService<ILogger<GameServer>>();
            var gameStateManager = app.Services.GetRequiredService<GameStateManager>();
            var gameServer = new GameServer(app, gameStateManager, logger);
            gameServer.Start();
        }
    }
}