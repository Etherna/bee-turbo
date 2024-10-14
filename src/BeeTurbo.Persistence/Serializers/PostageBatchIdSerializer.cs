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
using Etherna.MongoDB.Bson.Serialization;
using Etherna.MongoDB.Bson.Serialization.Serializers;

namespace Etherna.BeeTurbo.Persistence.Serializers
{
    public class PostageBatchIdSerializer : SerializerBase<PostageBatchId>
    {
        private readonly StringSerializer stringSerializer = new();

        public override PostageBatchId Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var batchId = stringSerializer.Deserialize(context, args);
            return PostageBatchId.FromString(batchId);
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, PostageBatchId value)
        {
            stringSerializer.Serialize(context, args, value.ToString());
        }
    }
}