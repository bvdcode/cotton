// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Routines;
using Cotton.Server.Models.Dto;
using EasyExtensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController, Authorize, Route("api/v1/routines")]
    public class RoutinesController(IMediator mediator) : ControllerBase
    {
        [HttpGet("options")]
        public async Task<IActionResult> Options(CancellationToken ct)
            => Ok(await mediator.Send(new GetRoutineOptionsQuery(), ct));

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken ct)
            => Ok(await mediator.Send(new GetRoutinesQuery(User.GetUserId()), ct));

        [HttpGet("{id:guid}/runs")]
        public async Task<IActionResult> Runs(Guid id, CancellationToken ct)
            => Ok(await mediator.Send(new GetRoutineRunsQuery(User.GetUserId(), id), ct));

        [HttpPost]
        public async Task<IActionResult> Create(RoutineDraftDto draft, CancellationToken ct)
            => Ok(await mediator.Send(new SaveRoutineRequest(User.GetUserId(), null, draft), ct));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, RoutineDraftDto draft, CancellationToken ct)
            => Ok(await mediator.Send(new SaveRoutineRequest(User.GetUserId(), id, draft), ct));

        [HttpPost("{id:guid}/run")]
        public async Task<IActionResult> Run(Guid id, CancellationToken ct)
        {
            await mediator.Send(new RunRoutineNowRequest(User.GetUserId(), id), ct);
            return Accepted();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await mediator.Send(new DeleteRoutineRequest(User.GetUserId(), id), ct);
            return NoContent();
        }
    }
}
