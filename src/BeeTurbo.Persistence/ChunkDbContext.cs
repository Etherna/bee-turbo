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

using Etherna.BeeTurbo.Domain;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.GridFS;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Serialization;
using System.Collections.Generic;

namespace Etherna.BeeTurbo.Persistence
{
    public class ChunkDbContext : DbContext, IChunkDbContext
    {
        private GridFSBucket? _chunksBucket;
        
        protected override IEnumerable<IModelMapsCollector> ModelMapsCollectors => [];

        public GridFSBucket ChunksBucket
        {
            get
            {
                if (_chunksBucket == null)
                    _chunksBucket = new GridFSBucket(Database, new GridFSBucketOptions
                    {
                        BucketName = "chunks",
                        WriteConcern = WriteConcern.WMajority,
                        ReadPreference = ReadPreference.Secondary
                    });
                return _chunksBucket;
            }
        }
    }
}