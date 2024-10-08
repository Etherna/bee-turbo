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

using Etherna.BeeNet;
using Etherna.BeeNet.Models;
using Etherna.BeeTurbo.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Etherna.BeeTurbo
{
    public static class Program
    {
        static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            
            // Get config.
            var beeUrl = builder.Configuration["BeeUrl"] ??
                         throw new ArgumentException("BeeUrl is not defined");

            // Configs.
            ConfigureServices(builder, beeUrl);

            var app = builder.Build();
            ConfigureApplication(app, beeUrl);

            // Run application.
            app.Run();
        }
        
        private static void ConfigureServices(WebApplicationBuilder builder, string beeUrl)
        {
            var services = builder.Services;

            services.AddHttpForwarder();

            // Singleton services.
            services.AddSingleton<IBeeClient>(_ => new BeeClient(new Uri(beeUrl, UriKind.Absolute)));
            services.AddSingleton<IChunkStreamTurboProcessor, ChunkStreamTurboProcessor>();
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
        private static void ConfigureApplication(WebApplication app, string beeUrl)
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
                    httpContext.Request.Headers.TryGetValue(SwarmHttpConsts.SwarmPostageBatchId, out var batchIdHeaderValue);
                    httpContext.Request.Headers.TryGetValue(SwarmHttpConsts.SwarmTag, out var tagIdHeaderValue);
                    var batchId = PostageBatchId.FromString(batchIdHeaderValue.Single()!);
                    var tagIdStr = tagIdHeaderValue.SingleOrDefault();
                    TagId? tagId = tagIdStr is null ? null : new TagId(ulong.Parse(tagIdStr));
                    
                    // Get websocket.
                    var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
                    
                    await processer.HandleWebSocketConnection(
                        batchId,
                        tagId,
                        webSocket);
                }
                else
                {
                    httpContext.Response.StatusCode = 400;
                    await httpContext.Response.WriteAsync("Expected a WebSocket request");
                }
            });
            app.MapForwarder("/{**catch-all}", beeUrl);
        }
    }
}