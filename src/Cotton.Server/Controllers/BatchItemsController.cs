// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Server.Handlers.Files;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Authorize]
    [Route(Routes.V1.Base + "/items")]
    public class BatchItemsController(IMediator _mediator) : ControllerBase
    {
        [HttpPost("restore")]
        public async Task<IActionResult> Restore([FromBody] BatchItemsRequestDto request)
        {
            if (request.Items.Count == 0 || request.Items.Count > BatchItemLimits.RestoreCount)
            {
                return BadRequest($"Between 1 and {BatchItemLimits.RestoreCount} items are required.");
            }

            IReadOnlyList<BatchItemResultDto> result = await _mediator.Send(
                new RestoreBatchItemsRequest(User.GetUserId(), request.Items),
                HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromBody] BatchItemsRequestDto request)
        {
            if (request.Items.Count == 0)
            {
                return BadRequest("Items are required.");
            }

            IReadOnlyList<BatchItemResultDto> result = await _mediator.Send(
                new DeleteBatchItemsRequest(User.GetUserId(), request.Items, request.SkipTrash),
                HttpContext.RequestAborted);
            return Ok(result);
        }
    }
}
