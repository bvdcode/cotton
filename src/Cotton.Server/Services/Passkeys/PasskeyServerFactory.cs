// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Providers;
using EasyExtensions.Passkeys;

namespace Cotton.Server.Services.Passkeys
{
    public class PasskeyServerFactory(SettingsProvider settings) : IPasskeyServerFactory
    {
        public async Task<IPasskeyServer> CreateAsync(CancellationToken cancellationToken)
        {
            Uri publicBaseUri = new(await settings.GetPublicBaseUrlAsync(cancellationToken), UriKind.Absolute);
            return new PasskeyServer(new PasskeyServerOptions(
                publicBaseUri.IdnHost,
                Constants.ProductName,
                [publicBaseUri.GetLeftPart(UriPartial.Authority)]));
        }
    }
}
