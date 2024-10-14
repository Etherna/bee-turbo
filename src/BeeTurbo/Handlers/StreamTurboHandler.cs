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
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeeTurbo.Handlers
{
    internal sealed class StreamTurboHandler(
        IChunkStreamTurboProcessor processer)
        : IStreamTurboHandler
    {
        public async Task HandleAsync(HttpContext httpContext)
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
        }
    }
}