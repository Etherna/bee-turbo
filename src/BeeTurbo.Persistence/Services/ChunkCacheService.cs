using Etherna.BeeTurbo.Persistence.Options;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.Core.Configuration;
using Etherna.MongoDB.Driver.GridFS;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;

namespace Etherna.BeeTurbo.Persistence.Services;

public class ChunkCacheService : IChunkCacheService
{
    // Constructor.
    public ChunkCacheService(
        ILoggerFactory loggerFactory,
        IOptions<ChunkCacheServiceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        
        var mongoClientSettings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString);
        mongoClientSettings.ClusterConfigurator = cb =>
        {
            cb.ConfigureLoggingSettings(_ => new LoggingSettings(loggerFactory));
        };

        var mongoClient = new MongoClient(mongoClientSettings);
        var database = mongoClient.GetDatabase(options.Value.DbName);

        ChunksBucket = new GridFSBucket(database, new GridFSBucketOptions
        {
            BucketName = "chunks",
            WriteConcern = WriteConcern.WMajority,
            ReadPreference = ReadPreference.Secondary
        });
    }

    // Properties.
    public GridFSBucket ChunksBucket { get; private set; }
}