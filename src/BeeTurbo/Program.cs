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
using Etherna.BeeTurbo.Persistence.Options;
using Etherna.BeeTurbo.Persistence.Services;
using Etherna.BeeTurbo.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.Elasticsearch;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Etherna.BeeTurbo
{
    public static class Program
    {
        static void Main(string[] args)
        {
            // Configure logging first.
            ConfigureLogging();

            // Then create the host, so that if the host fails we can log errors.
            try
            {
                Log.Information("Starting web host");

                var builder = WebApplication.CreateBuilder(args);

                // Configs.
                builder.Host.UseSerilog();
                var beeUrl = builder.Configuration["BeeUrl"] ??
                             throw new ArgumentException("BeeUrl is not defined");

                ConfigureServices(builder, beeUrl);

                var app = builder.Build();
                ConfigureApplication(app, beeUrl);

                // Run application.
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
        
        // Helpers.
        private static ElasticsearchSinkOptions ConfigureElasticSink(IConfigurationRoot configuration, string env)
        {
            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name!.ToLower(CultureInfo.InvariantCulture).Replace(".", "-", StringComparison.InvariantCulture);
            string envName = env.ToLower(CultureInfo.InvariantCulture).Replace(".", "-", StringComparison.InvariantCulture);
            return new ElasticsearchSinkOptions(configuration.GetSection("Elastic:Urls").Get<string[]>()!.Select(u => new Uri(u)))
            {
                AutoRegisterTemplate = true,
                IndexFormat = $"{assemblyName}-{envName}-{DateTime.UtcNow:yyyy-MM}"
            };
        }
        
        private static void ConfigureLogging()
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? throw new ArgumentException();
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails()
                .Enrich.WithMachineName()
                .WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                .WriteTo.Elasticsearch(ConfigureElasticSink(configuration, env))
                .Enrich.WithProperty("Environment", env)
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
        }
        
        private static void ConfigureServices(WebApplicationBuilder builder, string beeUrl)
        {
            var services = builder.Services;
            var config = builder.Configuration;

            services.AddHttpForwarder();
            
            // Configurations.
            services.Configure<ChunkCacheServiceOptions>(options =>
            {
                options.ConnectionString = config["ConnectionStrings:ChunkCacheDb"] ??
                                           throw new ArgumentException("ChunkCacheDb connection string is not defined");
                options.DbName = "ChunkCacheDb";
            });

            // Singleton services.
            services.AddSingleton<IBeeClient>(_ => new BeeClient(new Uri(beeUrl, UriKind.Absolute)));
            services.AddSingleton<IChunkCacheService, ChunkCacheService>();
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