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

using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.BeeTurbo.Tools
{
    public class ChunkStreamTurboServer : IChunkStreamTurboServer
    {
        public async Task StartWebSocketServer()
        {
            using var httpListener = new System.Net.HttpListener();
            httpListener.Prefixes.Add("http://localhost:5000/");
            httpListener.Start();

            Console.WriteLine("WebSocket server started at ws://localhost:5000/");

            while (true)
            {
                var httpContext = await httpListener.GetContextAsync();

                if (httpContext.Request.IsWebSocketRequest)
                {
                    var wsContext = await httpContext.AcceptWebSocketAsync(null);
                    _ = HandleWebSocketConnection(wsContext.WebSocket);
                }
                else
                {
                    httpContext.Response.StatusCode = 400;
                    httpContext.Response.Close();
                }
            }
        }

        public async Task HandleWebSocketConnection(WebSocket webSocket)
        {
            ArgumentNullException.ThrowIfNull(webSocket, nameof(webSocket));
            
            var buffer = new byte[1024 * 4];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Received: {message}");

                    // Echo the message back to the client
                    var serverMessage = "Message received"u8.ToArray();
                    await webSocket.SendAsync(new ArraySegment<byte>(serverMessage), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    Console.WriteLine("WebSocket connection closed.");

                    // Example action on collected data
                    Console.WriteLine("Performing example action with collected data.");
                }
            }
        }
    }
}