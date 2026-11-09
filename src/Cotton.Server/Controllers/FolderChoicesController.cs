// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Nodes;
using EasyExtensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController, Authorize, Route("api/v1/layouts/nodes/{nodeId:guid}/folders")]
    public class FolderChoicesController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get(Guid nodeId, [FromQuery] int page = 1, CancellationToken ct = default)
            => Ok(await mediator.Send(new GetFolderChoicesQuery(User.GetUserId(), nodeId, page), ct));
    }
}
