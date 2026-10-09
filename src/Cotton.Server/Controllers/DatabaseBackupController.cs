// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Handlers.Server;
using EasyExtensions.Mediator;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Server + "/database-backup")]
    public class DatabaseBackupController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> GetHistory(CancellationToken cancellationToken) =>
            Ok(await mediator.Send(new GetDatabaseBackupHistoryQuery(), cancellationToken));

        [HttpPost]
        [Authorize(AuthenticationSchemes = DatabaseBackupAuthenticationHandler.SchemeName)]
        public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
            Ok(await mediator.Send(new CreateDatabaseBackupRequest(), cancellationToken));

        [HttpPost("token")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> CreateToken(CancellationToken cancellationToken)
        {
            Response.Headers.CacheControl = "no-store";
            return Ok(await mediator.Send(new CreateDatabaseBackupTokenRequest(), cancellationToken));
        }
    }
}
