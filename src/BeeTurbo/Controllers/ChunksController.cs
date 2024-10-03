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

using Etherna.BeeTurbo.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Etherna.BeeTurbo.Controllers;

[ApiController]
[Route("[controller]")]
public class ChunksController(IChunksControllerService service) : ControllerBase
{
    [HttpGet("stream-turbo")]
    public Task Get() =>
        service.HandleStreamTurboRequestAsync(HttpContext);
}