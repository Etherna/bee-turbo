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
using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeeTurbo.Handlers
{
    internal sealed class ChunksBulkUploadHandler(
        IBeehiveDbContext dbContext)
        : IChunksBulkUploadHandler
    {
        // Methods.
        [SuppressMessage("ReSharper", "EmptyGeneralCatchClause")]
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        public async Task HandleAsync(HttpContext httpContext)
        {
            if (httpContext.Request.Method == "POST")
            {
                try
                {
                    // Get headers.
                    httpContext.Request.Headers.TryGetValue(
                        SwarmHttpConsts.SwarmPostageBatchId,
                        out var batchIdHeaderValue);
                    var batchId = PostageBatchId.FromString(batchIdHeaderValue.Single()!);

                    // Consume data from request.
                    await using var memoryStream = new MemoryStream();
                    await httpContext.Request.Body.CopyToAsync(memoryStream);
                    var payload = memoryStream.ToArray();

                    var hasher = new Hasher();
                    for (int i = 0; i < payload.Length;)
                    {
                        //read chunk size
                        var chunkSize = ReadUshort(payload.AsSpan()[i..(i + sizeof(ushort))]);
                        i += sizeof(ushort);
                        if (chunkSize > SwarmChunk.SpanAndDataSize)
                            throw new InvalidOperationException();

                        //read and store chunk payload
                        var chunkPayload = payload[i..(i + chunkSize)];
                        i += chunkSize;
                        var hash = SwarmChunkBmtHasher.Hash(
                            chunkPayload[..SwarmChunk.SpanSize].ToArray(),
                            chunkPayload[SwarmChunk.SpanSize..].ToArray(),
                            hasher);
                        var chunkRef = new UploadedChunkRef(hash, batchId);

                        //read check hash
                        var checkHash = ReadSwarmHash(payload.AsSpan()[i..(i + SwarmHash.HashSize)]);
                        i += SwarmHash.HashSize;
                        if (checkHash != hash)
                            throw new InvalidDataException("Invalid hash with provided data");

                        await dbContext.ChunksBucket.UploadFromBytesAsync(hash.ToString(), chunkPayload);
                        await dbContext.ChunkPushQueue.CreateAsync(chunkRef);
                    }

                    // Reply.
                    httpContext.Response.StatusCode = StatusCodes.Status201Created;
                }
                catch(InvalidDataException)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
            }
        }
        
        // Helpers.
        private static SwarmHash ReadSwarmHash(Span<byte> payload)
        {
            if (payload.Length != SwarmHash.HashSize)
                throw new ArgumentOutOfRangeException(nameof(payload));
            
            var valueByteArray = new byte[SwarmHash.HashSize];
            for (int i = 0; i < valueByteArray.Length; i++)
                valueByteArray[i] = payload[i];
            return SwarmHash.FromByteArray(valueByteArray);
        }
        
        private static ushort ReadUshort(Span<byte> payload)
        {
            if (payload.Length != sizeof(ushort))
                throw new ArgumentOutOfRangeException(nameof(payload));
            
            var valueByteArray = new byte[sizeof(ushort)];
            for (int i = 0; i < valueByteArray.Length; i++)
                valueByteArray[i] = payload[i];
            return BitConverter.ToUInt16(valueByteArray);
        }
    }
}