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
    public class SwarmHashSerializer : SerializerBase<SwarmHash>
    {
        private readonly StringSerializer stringSerializer = new();
        
        public override SwarmHash Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var hash = stringSerializer.Deserialize(context, args);
            return hash is null ?
                SwarmHash.Zero :
                SwarmHash.FromString(hash);
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, SwarmHash value)
        {
            stringSerializer.Serialize(context, args, value.ToString());
        }
    }
}