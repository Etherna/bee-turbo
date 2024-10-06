// Copyright 2024-present Etherna SA
// This file is part of BeeTurbo.
// 
// BeeTurbo is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeeTurbo is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeeTurbo.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.BeeNet.Models;
using Etherna.BeeTurbo.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Etherna.BeeTurbo
{
    public static class Program
    {
        public const string LocalBeeNodeAddress = "http://localhost:1633/";
        
        static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configs.
            ConfigureServices(builder);

            var app = builder.Build();
            ConfigureApplication(app);

            // Run application.
            app.Run();
        }
        
        private static void ConfigureServices(WebApplicationBuilder builder)
        {
            var services = builder.Services;

            services.AddHttpForwarder();

            // Singleton services.
            services.AddSingleton<IChunkStreamTurboProcessor, ChunkStreamTurboProcessor>();
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
        private static void ConfigureApplication(WebApplication app)
        {
            app.UseRouting();
            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromMinutes(10)
            });

            // Configure endpoint mapping
            app.Map("/chunks/stream-turbo", async (HttpContext httpContext, IChunkStreamTurboProcessor processer) =>
            {
                if (httpContext.WebSockets.IsWebSocketRequest)
                {
                    // Get headers.
                    //TODO
                    
                    // Get websocket.
                    var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
                    
                    await processer.HandleWebSocketConnection(
                        PostageBatchId.Zero,
                        null,
                        webSocket);
                }
                else
                {
                    httpContext.Response.StatusCode = 400;
                    await httpContext.Response.WriteAsync("Expected a WebSocket request");
                }
            });
            app.MapForwarder("/{**catch-all}", LocalBeeNodeAddress);
        }
    }
}