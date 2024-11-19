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

using Etherna.BeeNet.Hashing.Store;
using Etherna.BeeNet.Models;
using Etherna.BeeTurbo.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace Etherna.BeeTurbo.Handlers
{
    internal sealed class ChunksHandler(
        IChunkStore chunkStore,
        IHttpForwarder forwarder,
        IOptions<ForwarderOptions> options)
        : IChunksHandler
    {
        // Fields.
        private readonly ForwarderOptions options = options.Value;

        // Methods.
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        public async Task<IResult> HandleAsync(HttpContext httpContext, string hash)
        {
            if (httpContext.Request.Method == "GET")
            {
                try
                {
                    var swarmHash = SwarmHash.FromString(hash);
                    var chunk = await chunkStore.GetAsync(swarmHash);

                    return Results.File(
                        chunk.GetSpanAndData(),
                        "application/octet-stream",
                        hash);
                }
                catch { } //proceed with forward on any error
            }
            
            using var socketsHttpHandler = new SocketsHttpHandler();
            using var httpClient = new HttpMessageInvoker(socketsHttpHandler);
            var error = await forwarder.SendAsync(
                httpContext,
                options.BeeUrl,
                httpClient);
                    
            if (error != ForwarderError.None)
            {
                httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
                await httpContext.Response.WriteAsync("An error occurred while forwarding the request.");
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
                    
            return null!;
        }
    }
}