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

using Etherna.BeeTurbo.Services;
using Etherna.BeeTurbo.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Etherna.BeeTurbo
{
    public static class Program
    {
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

            services.AddControllers();

            // Scoped services.
            services.AddScoped<IChunksControllerService, ChunksControllerService>();

            // Singleton services.
            services.AddSingleton<IChunkStreamTurboServer, ChunkStreamTurboServer>();
        }

        private static void ConfigureApplication(WebApplication app)
        {
            app.UseHttpsRedirection();

            app.UseRouting();
            app.MapControllers();
            app.UseWebSockets();
        }
    }
}