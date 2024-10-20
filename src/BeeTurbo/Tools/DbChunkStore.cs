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
using Etherna.BeeTurbo.Domain;
using Etherna.BeeTurbo.Domain.Models;
using Etherna.MongODM.Core.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeeTurbo.Tools
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    internal sealed class DbChunkStore(
        IBeehiveDbContext dbContext)
        : IChunkStore
    {
        public async Task<SwarmChunk> GetAsync(SwarmHash hash, SwarmHash? rootHash)
        {
            using var dbExecContextHandler = new DbExecutionContextHandler(dbContext);

            var chunk = await dbContext.Chunks.TryFindOneAsync(hash);
            byte[]? payload = null;
            if (chunk is not null)
                payload = chunk.Payload.ToArray();
            payload ??= await dbContext.ChunksBucket.DownloadAsBytesByNameAsync(hash.ToString());
            
            return SwarmChunk.BuildFromSpanAndData(hash, payload);
        }

        public async Task<SwarmChunk?> TryGetAsync(SwarmHash hash, SwarmHash? rootHash)
        {
            try
            {
                return await GetAsync(hash, rootHash);
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> AddAsync(SwarmChunk chunk)
        {
            try
            {
                var domainChunk = new Chunk(chunk.Hash, chunk.GetSpanAndData());
                await dbContext.Chunks.CreateAsync(domainChunk);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}