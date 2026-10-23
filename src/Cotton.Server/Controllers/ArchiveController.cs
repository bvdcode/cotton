// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Archives;
using Cotton.Server.Models.Requests;
using Cotton.Server.Models.Results;
using Cotton.Server.Services;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Archives)]
    public class ArchiveController(
        IMediator _mediator,
        ArchiveDownloadTicketStore _tickets) : ControllerBase
    {
        [Authorize]
        [HttpPost("download-link")]
        public async Task<IActionResult> CreateDownloadLink(
            [FromBody] CreateArchiveDownloadLinkRequest request,
            CancellationToken cancellationToken)
        {
            CreateArchiveDownloadLinkResult result = await _mediator.Send(
                new CreateArchiveDownloadLinkQuery(User.GetUserId(), request),
                cancellationToken);

            return result.StatusCode switch
            {
                StatusCodes.Status200OK => Ok(result.Link),
                StatusCodes.Status400BadRequest => BadRequest(result.Error),
                StatusCodes.Status404NotFound => NotFound(result.Error),
                _ => StatusCode(result.StatusCode, result.Error),
            };
        }

        [AllowAnonymous]
        [HttpGet("{token}")]
        [HttpHead("{token}")]
        public IActionResult Download([FromRoute] string token)
        {
            if (!_tickets.TryGet(token, out ArchiveDownloadTicket? ticket))
            {
                return NotFound("Archive download link not found.");
            }
            return new ArchiveDownloadResult(ticket);
        }
    }
}
