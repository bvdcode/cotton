// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class YieldingGeoLookupService : IGeoLookupService
    {
        public async Task<GeoLookupResult?> TryLookupAsync(
            IPAddress ipAddress, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return new GeoLookupResult("Country", "Region", "City");
        }

        public Task<CustomGeoLookupTestResult> TestCustomLookupAsync(
            string serverBaseUrl, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
