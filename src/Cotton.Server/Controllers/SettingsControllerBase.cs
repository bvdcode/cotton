// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Helpers;
using Cotton.Server.Providers;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    public abstract class SettingsControllerBase(SettingsProvider settings) : ControllerBase
    {
        protected SettingsProvider Settings { get; } = settings;

        protected async Task EnsureSettingsAsync(CancellationToken cancellationToken)
        {
            await Settings.EnsureServerSettingsAsync(GetFallbackPublicBaseUrl(), cancellationToken);
        }

        protected string GetFallbackPublicBaseUrl()
        {
            ServerSettingsSnapshot settingsSnapshot = Settings.GetServerSettings();
            return RequestBaseUrlHelpers.GetBaseUrl(
                Request,
                settingsSnapshot.TrustedProxyIpAddress,
                settingsSnapshot.TrustedProxyPrefixLength);
        }

        protected static void ThrowIfInvalid(string? error)
        {
            if (error is not null)
            {
                throw new BadRequestException<CottonServerSettings>(error);
            }
        }
    }
}
