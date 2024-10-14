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

using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Hashing.Bmt;
using Etherna.BeeNet.Models;
using Etherna.BeeTurbo.Domain;
using Etherna.BeeTurbo.Domain.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.BeeTurbo.Tools
{
    public class ChunkStreamTurboProcessor(
        // IBeeClient beeClient,
        IBeehiveDbContext dbContext)
        : IChunkStreamTurboProcessor
    {
        // Consts.
        private const int WebsocketInternalBufferSize = 1024 * 1024 * 10; //10MB
        
        // Fields.
        private ushort? nextChunkSize;

        // Methods.
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        public async Task HandleWebSocketConnection(
            PostageBatchId batchId,
            TagId? tagId,
            WebSocket clientWebsocket)
        {
            ArgumentNullException.ThrowIfNull(clientWebsocket, nameof(clientWebsocket));
            
            /*
             * Use http instead of websocket as workaround for https://github.com/ethersphere/bee/issues/4858
             */
            // // Open websocket with Bee.
            // using var beeWebsocket = await beeClient.GetChunkUploaderWebSocketAsync(
            //     batchId, tagId, CancellationToken.None);

            // Consume data from client and push to Bee.
            var internalBuffer = new byte[WebsocketInternalBufferSize];
            var receivedDataQueue = new Queue<byte>();
            var hasher = new Hasher();
            try
            {
                while (clientWebsocket.State == WebSocketState.Open)
                {
                    // Receive data.
                    var isLastBatch = await ReceiveDataAsync(clientWebsocket, internalBuffer, receivedDataQueue);

                    if (!isLastBatch)
                    {
                        // Process data.
                        // await ProcessDataAsync(beeWebsocket, receivedDataQueue);
                        await ProcessDataAsync(batchId, tagId, receivedDataQueue, hasher);

                        var ackBytes = "ack"u8.ToArray();
                        await clientWebsocket.SendAsync(ackBytes, WebSocketMessageType.Binary, false, CancellationToken.None);
                    }
                    else
                    {
                        await clientWebsocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", default);
                    }
                }
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case InvalidOperationException _:
                        await clientWebsocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Protocol error", default);
                        break;
                    default:
                        await clientWebsocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Internal error", default);
                        break;
                }
            }
            // finally
            // {
            //     await beeWebsocket.CloseAsync();
            // }
        }

        // Helpers.
        // /// <summary>
        // /// Consume data from client and feed Bee with it
        // /// </summary>
        // /// <param name="beeWebsocket">The websocket opened with bee instance</param>
        // /// <param name="dataQueue">Data received from client</param>
        // /// <returns>True if the protocol is completed, false otherwise</returns>
        // private async Task ProcessDataAsync(IChunkWebSocketUploader beeWebsocket, Queue<byte> dataQueue)
        // {
        //     while (dataQueue.Count > 0)
        //     {
        //         //read next chunk size
        //         if (!nextChunkSize.HasValue)
        //         {
        //             if (TryReadUshort(dataQueue, out var ncs))
        //             {
        //                 if (ncs > SwarmChunk.SpanAndDataSize)
        //                     throw new InvalidOperationException();
        //                 nextChunkSize = ncs;
        //             }
        //             else break;
        //         }
        //
        //         //read chunk payload
        //         if (TryReadByteArray(dataQueue, nextChunkSize.Value, out var chunkPayload))
        //         {
        //             await beeWebsocket.SendChunkAsync(chunkPayload, CancellationToken.None);
        //             nextChunkSize = null;
        //         }
        //         else break;
        //     }
        // }
        
        /// <summary>
        /// Consume data from client and feed Bee with it
        /// </summary>
        /// <param name="dataQueue">Data received from client</param>
        /// <returns>True if the protocol is completed, false otherwise</returns>
        private async Task ProcessDataAsync(
            PostageBatchId batchId,
            TagId? tagId,
            Queue<byte> dataQueue,
            IHasher hasher)
        {
            while (dataQueue.Count > 0)
            {
                //read next chunk size
                if (!nextChunkSize.HasValue)
                {
                    if (TryReadUshort(dataQueue, out var ncs))
                    {
                        if (ncs > SwarmChunk.SpanAndDataSize)
                            throw new InvalidOperationException();
                        nextChunkSize = ncs;
                    }
                    else break;
                }

                //read and store chunk payload
                if (TryReadByteArray(dataQueue, nextChunkSize.Value, out var chunkPayload))
                {
                    var hash = SwarmChunkBmtHasher.Hash(
                        chunkPayload[..SwarmChunk.SpanSize].ToArray(),
                        chunkPayload[SwarmChunk.SpanSize..].ToArray(),
                        hasher);
                    var chunkRef = new UploadedChunkRef(hash, batchId);

                    await dbContext.ChunksBucket.UploadFromBytesAsync(hash.ToString(), chunkPayload);
                    await dbContext.ChunkPushQueue.CreateAsync(chunkRef);
                    
                    nextChunkSize = null;
                }
                else break;
            }
        }

        private static async Task<bool> ReceiveDataAsync(WebSocket webSocket, byte[] wsBuffer, Queue<byte> dataQueue)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(wsBuffer), CancellationToken.None);
            switch (result.MessageType)
            {
                case WebSocketMessageType.Close:
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                case WebSocketMessageType.Binary:
                    break;
                default:
                    await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Protocol error", CancellationToken.None);
                    break;
            }

            for (int i = 0; i < result.Count; i++)
                dataQueue.Enqueue(wsBuffer[i]);

            return result.EndOfMessage;
        }

        private static bool TryReadByteArray(Queue<byte> dataQueue, int dataSize, out byte[] value)
        {
            if (dataQueue.Count < dataSize)
            {
                value = [];
                return false;
            }
            
            value = new byte[dataSize];
            for (int i = 0; i < dataSize; i++)
                value[i] = dataQueue.Dequeue();
            return true;
        }

        private static bool TryReadUshort(Queue<byte> dataQueue, out ushort value)
        {
            if (dataQueue.Count < sizeof(ushort))
            {
                value = 0;
                return false;
            }
            
            var valueByteArray = new byte[sizeof(ushort)];
            for (int i = 0; i < sizeof(ushort); i++)
                valueByteArray[i] = dataQueue.Dequeue();
            value = BitConverter.ToUInt16(valueByteArray);
            return true;
        }
    }
}