// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Helpers;
using Cotton.Server.Providers;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Provides request-aware operations shared by settings controllers.
    /// </summary>
    /// <param name="settings">The server settings provider.</param>
    public abstract class SettingsControllerBase(SettingsProvider settings) : ControllerBase
    {
        /// <summary>
        /// Gets the server settings provider.
        /// </summary>
        protected SettingsProvider Settings { get; } = settings;

        /// <summary>
        /// Ensures the server settings record exists.
        /// </summary>
        protected async Task EnsureSettingsAsync(CancellationToken cancellationToken)
        {
            await Settings.EnsureServerSettingsAsync(GetFallbackPublicBaseUrl(), cancellationToken);
        }

        /// <summary>
        /// Gets the externally visible base URL for the current request.
        /// </summary>
        protected string GetFallbackPublicBaseUrl()
        {
            ServerSettingsSnapshot settingsSnapshot = Settings.GetServerSettings();
            return RequestBaseUrlHelpers.GetBaseUrl(
                Request,
                settingsSnapshot.TrustedProxyIpAddress,
                settingsSnapshot.TrustedProxyPrefixLength);
        }

        /// <summary>
        /// Converts a settings validation error into a bad-request exception.
        /// </summary>
        protected static void ThrowIfInvalid(string? error)
        {
            if (error is not null)
            {
                throw new BadRequestException<CottonServerSettings>(error);
            }
        }
    }
}
