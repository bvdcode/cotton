// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Providers;
using Fido2NetLib;

namespace Cotton.Server.Services.Passkeys
{
    public class PasskeyClientFactory(SettingsProvider settings) : IPasskeyClientFactory
    {
        public async Task<IFido2> CreateAsync(CancellationToken cancellationToken)
        {
            Uri publicBaseUri = new(await settings.GetPublicBaseUrlAsync(cancellationToken), UriKind.Absolute);
            return new Fido2(new Fido2Configuration
            {
                ServerDomain = publicBaseUri.Host,
                ServerName = Constants.ProductName,
                Origins = new HashSet<string> { publicBaseUri.GetLeftPart(UriPartial.Authority) },
                Timeout = 60_000,
                ChallengeSize = 32
            }, metadataService: null);
        }
    }
}
