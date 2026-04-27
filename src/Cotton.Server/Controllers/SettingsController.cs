using Cotton.Server.Handlers.Server;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Models.Enums;
using Mapster;
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
        public async Task<IActionResult> CreateSettings(CottonServerSettingsDto request, CancellationToken cancellationToken)
        {
            string fallbackPublicBaseUrl = $"{Request.Scheme}://{Request.Host.Value}";
            await _mediator.Send(new CreateInitialServerSettingsRequest(request, fallbackPublicBaseUrl), cancellationToken);
            return Ok();
        }

        [Authorize]
        [HttpGet("chunk-size")]
        public async Task<IActionResult> GetChunkSize()
        {
            int maxChunkSizeBytes = _settings.GetServerSettings().MaxChunkSizeBytes;
            return Ok(new { maxChunkSizeBytes });
        }

        [Authorize]
        [HttpGet("supported-hash-algorithms")]
        public async Task<IActionResult> GetSupportedHashAlgorithms()
        {
            return Ok(new { supportedHashAlgorithms = new string[] { Hasher.SupportedHashAlgorithm } });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet]
        public async Task<CottonServerSettingsDto> GetSettings()
        {
            return _settings.GetServerSettings().Adapt<CottonServerSettingsDto>();
        }
    }
}
