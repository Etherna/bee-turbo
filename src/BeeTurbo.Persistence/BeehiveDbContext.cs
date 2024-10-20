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
using Etherna.BeeTurbo.Domain;
using Etherna.BeeTurbo.Domain.Models;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.GridFS;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Repositories;
using Etherna.MongODM.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Etherna.BeeTurbo.Persistence
{
    public class BeehiveDbContext : DbContext, IBeehiveDbContext
    {
        // Consts.
        private const string ModelMapsNamespace = "Etherna.BeeTurbo.Persistence.ModelMaps";
        
        // Fields.
        private GridFSBucket? _chunksBucket;
        
        // Properties.
        //repositories
        public IRepository<UploadedChunkRef, string> ChunkPushQueue { get; } =
            new Repository<UploadedChunkRef, string>("chunkPushQueue");
        public IRepository<Chunk, SwarmHash> Chunks { get; } =
            new Repository<Chunk, SwarmHash>(new RepositoryOptions<Chunk>("chunks")
            {
                IndexBuilders =
                [
                    (Builders<Chunk>.IndexKeys.Ascending(c => c.CreationDateTime), new CreateIndexOptions<Chunk>())
                ]
            });
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
        
        //other properties
        protected override IEnumerable<IModelMapsCollector> ModelMapsCollectors =>
            from t in typeof(BeehiveDbContext).GetTypeInfo().Assembly.GetTypes()
            where t.IsClass && t.Namespace == ModelMapsNamespace
            where t.GetInterfaces().Contains(typeof(IModelMapsCollector))
            select Activator.CreateInstance(t) as IModelMapsCollector;
    }
}