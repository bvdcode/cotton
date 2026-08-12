// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Settings)]
    [Route(Routes.V1.Server + "/settings")]
    public class EmailSettingsController(
        SettingsProvider settings,
        ServerSettingsValidator _validator,
        INotificationsProvider _notifications) : SettingsControllerBase(settings)
    {
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("email-mode/{mode}")]
        public async Task<IActionResult> SetEmailMode(
            [FromRoute] EmailMode mode,
            CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(await _validator.ValidateEmailModeAsync(mode, cancellationToken));
            await Settings.SetPropertyAsync(
                x => x.EmailMode,
                mode,
                GetFallbackPublicBaseUrl(),
                cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("email-mode")]
        public IActionResult GetEmailMode()
        {
            EmailMode emailMode = Settings.GetServerSettings().EmailMode;
            return Ok(new { emailMode = emailMode.ToString() });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("email-config")]
        public async Task<IActionResult> SetEmailConfig(
            [FromBody] EmailConfig emailConfig,
            CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_validator.ValidateEmailConfig(emailConfig));
            if (!ServerSettingsValidator.TryParsePort(emailConfig.Port, out int smtpPort))
            {
                return this.ApiBadRequest("Invalid SMTP port number.");
            }

            await Settings.UpdateSettingsAsync(settings =>
            {
                settings.SmtpServerAddress = emailConfig.SmtpServer.Trim();
                settings.SmtpServerPort = smtpPort;
                settings.SmtpUsername = emailConfig.Username.Trim();
                settings.SmtpPasswordEncrypted = emailConfig.Password;
                settings.SmtpSenderEmail = emailConfig.FromAddress.Trim();
                settings.SmtpUseSsl = emailConfig.UseSSL;
            }, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPost("email-config/test")]
        public async Task<IActionResult> SendEmailConfigTest(CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(await _validator.ValidateEmailModeAsync(EmailMode.Custom, cancellationToken));

            Guid userId = User.GetUserId();
            try
            {
                await _notifications.SendSmtpTestEmailAsync(userId, GetFallbackPublicBaseUrl());
            }
            catch (Exception ex)
            {
                throw new BadRequestException<CottonServerSettings>("Failed to send test email: " + ex.Message);
            }
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("email-config")]
        public IActionResult GetEmailConfig()
        {
            ServerSettingsSnapshot settings = Settings.GetServerSettings();
            EmailConfig emailConfig = new()
            {
                Username = settings.SmtpUsername ?? string.Empty,
                Password = string.Empty,
                SmtpServer = settings.SmtpServerAddress ?? string.Empty,
                Port = settings.SmtpServerPort?.ToString() ?? string.Empty,
                FromAddress = settings.SmtpSenderEmail ?? string.Empty,
                UseSSL = settings.SmtpUseSsl,
            };
            return Ok(emailConfig);
        }
    }
}
