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
using Etherna.BeeTurbo.Persistence.Serializers;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Serialization;

namespace Etherna.BeeTurbo.Persistence.ModelMaps
{
    internal sealed class BeeNetMap : IModelMapsCollector
    {
        public void Register(IDbContext dbContext)
        {
            dbContext.MapRegistry.AddModelMap<PostageBatchId>( //v0.2.0
                "57e606db-94be-4b3f-85fc-193483020012",
                customSerializer: new PostageBatchIdSerializer());
            
            dbContext.MapRegistry.AddModelMap<SwarmAddress>( //v0.2.0
                "5d4d4d6a-5448-43ca-aa74-50c6d0d82f25",
                customSerializer: new SwarmAddressSerializer());
            
            dbContext.MapRegistry.AddModelMap<SwarmHash>( //v0.2.0
                "64231e24-1cfc-4b24-8c7a-20f0f708969a",
                customSerializer: new SwarmHashSerializer());

            dbContext.MapRegistry.AddModelMap<SwarmUri>( //v0.2.0
                "99d8f98f-cfa4-4490-aeda-5dc2df7fdba8",
                customSerializer: new SwarmUriSerializer());
        }
    }
}