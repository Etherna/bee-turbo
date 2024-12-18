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
using Etherna.BeeNet.Hashing.Store;
using Etherna.BeeTurbo.Domain;
using Etherna.BeeTurbo.Handlers;
using Etherna.BeeTurbo.Options;
using Etherna.BeeTurbo.Persistence;
using Etherna.BeeTurbo.Tools;
using Etherna.MongODM;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            var config = builder.Configuration;
            var services = builder.Services;

            // Add services.
            services.AddCors();
            services.AddHttpForwarder();
            
            // Add request handlers.
            services.AddScoped<IBzzHandler, BzzHandler>();
            services.AddScoped<IChunksBulkUploadHandler, ChunksBulkUploadHandler>();
            services.AddScoped<IChunksHandler, ChunksHandler>();
            
            // Configure options.
            services.Configure<ForwarderOptions>(options =>
            {
                options.BeeUrl = beeUrl;
            });
            
            // Configure persistence.
            services.AddMongODMWithHangfire(configureHangfireOptions: options =>
                {
                    options.ConnectionString = config["ConnectionStrings:HangfireDb"] ??
                                               throw new ArgumentException("Hangfire connection string is not defined");
                    options.StorageOptions = new MongoStorageOptions
                    {
                        MigrationOptions = new MongoMigrationOptions //don't remove, could throw exception
                        {
                            MigrationStrategy = new MigrateMongoMigrationStrategy(),
                            BackupStrategy = new CollectionMongoBackupStrategy()
                        }
                    };
                })
                .AddDbContext<IBeehiveDbContext, BeehiveDbContext>(_ => new BeehiveDbContext(),
                    options =>
                    {
                        options.ConnectionString = config["ConnectionStrings:BeehiveDb"] ??
                                                   throw new ArgumentException("BeehiveDb connection string is not defined");
                    });

            // Singleton services.
            services.AddSingleton<IBeeClient>(_ => new BeeClient(new Uri(beeUrl, UriKind.Absolute)));
            services.AddSingleton<IChunkStore, DbChunkStore>();
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
        private static void ConfigureApplication(WebApplication app, string beeUrl)
        {
            var env = app.Environment;

            app.UseCors(builder =>
            {
                if (env.IsDevelopment())
                {
                    builder.SetIsOriginAllowed(_ => true)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
                else
                {
                    builder.WithOrigins("https://etherna.io")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            });
            
            app.UseRouting();
            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromMinutes(10)
            });

            // Configure endpoint mapping
            app.Map("/bzz/{*address:minlength(1)}", (HttpContext httpContext, string address, IBzzHandler handler) =>
                handler.HandleAsync(httpContext, address));

            app.MapForwarder("/chunks/stream", beeUrl);

            app.Map("/chunks/{*hash:length(64)}", (HttpContext httpContext, string hash, IChunksHandler handler) =>
                handler.HandleAsync(httpContext, hash));
            
            app.Map("/chunks/bulk-upload", (HttpContext httpContext, IChunksBulkUploadHandler handler) =>
                handler.HandleAsync(httpContext));
            
            app.MapForwarder("/{**catch-all}", beeUrl);
            
            // Internal features mapping
            app.Map("db/migrate", (IBeehiveDbContext dbContext) =>
            {
                dbContext.Chunks.BuildIndexesAsync();
            });
        }
    }
}