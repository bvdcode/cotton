using Cotton.Server.Handlers.Server;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using EasyExtensions.Mediator;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Runtime;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Settings)]
    public class SettingsController(
        SettingsProvider _settings,
        IMediator _mediator) : ControllerBase
    {

        [HttpGet("is-setup-complete")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> IsServerInitialized()
        {
            bool isServerInitialized = await _settings.IsServerInitializedAsync();
            return Ok(new { IsServerInitialized = isServerInitialized });
        }

        [HttpPost]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> CreateSettings(InitialServerSettingsRequestDto request, CancellationToken cancellationToken)
        {
            string fallbackPublicBaseUrl = $"{Request.Scheme}://{Request.Host.Value}";
            await _mediator.Send(new CreateInitialServerSettingsRequest(request, fallbackPublicBaseUrl), cancellationToken);
            return Ok();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            bool isAdmin = User.IsInRole(nameof(UserRole.Admin));
            ServerSettingsEnvelopeDto settings = await _mediator.Send(new GetServerSettingsQuery(isAdmin));
            return Ok(settings);
        }
    }
}
