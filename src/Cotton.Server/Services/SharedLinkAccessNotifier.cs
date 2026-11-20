// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using EasyExtensions.AspNetCore.Extensions;
using Microsoft.Extensions.Caching.Memory;
using System.Net;

namespace Cotton.Server.Services
{
    public class SharedLinkAccessNotifier(
        IMemoryCache _cache,
        INotificationsProvider _notifications,
        IGeoLookupService _geoLookup) : ISharedLinkAccessNotifier
    {
        private static readonly KeyedAsyncGate<string> Gates = new(StringComparer.Ordinal);

        private static string BuildKey(Guid ownerId, Guid tokenId, string ip, string userAgent) =>
            $"shared-access:{ownerId:N}:{tokenId:N}:{ip}:{userAgent}";

        public async Task NotifyOnceAsync(Guid ownerId, Guid tokenId, string name, HttpContext httpContext, CancellationToken ct)
        {
            if (!HttpMethods.IsGet(httpContext.Request.Method))
            {
                return;
            }

            IPAddress requestIp = httpContext.Request.GetTrustedClientIPAddress();
            string ip = requestIp.ToString();
            string userAgent = httpContext.Request.Headers.UserAgent.ToString();

            string cacheKey = BuildKey(ownerId, tokenId, ip, userAgent);

            await using IAsyncDisposable gate = await Gates.EnterAsync(cacheKey, ct);
            if (_cache.TryGetValue(cacheKey, out _))
            {
                return;
            }

            await _notifications.SendSharedLinkOpenedNotificationAsync(
                _geoLookup,
                ownerId,
                name,
                requestIp,
                httpContext.Request.Headers.UserAgent);

            _cache.Set(cacheKey, true, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });
        }
    }
}
