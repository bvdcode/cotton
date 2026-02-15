// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using EasyExtensions.AspNetCore.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Services
{
    public sealed class SharedFileDownloadNotifier(
        IMemoryCache _cache,
        INotificationsProvider _notifications) : ISharedFileDownloadNotifier
    {
        private static string BuildKey(Guid ownerId, Guid tokenId, string ip, string userAgent) =>
            $"shared-download:{ownerId:N}:{tokenId:N}:{ip}:{userAgent}";

        public async Task NotifyOnceAsync(Guid ownerId, Guid tokenId, string fileName, HttpContext httpContext, CancellationToken ct)
        {
            string ip = httpContext.Request.GetRemoteIPAddress().ToString();
            string userAgent = httpContext.Request.Headers.UserAgent.ToString();

            string cacheKey = BuildKey(ownerId, tokenId, ip, userAgent);

            if (_cache.TryGetValue(cacheKey, out _))
            {
                return;
            }

            _cache.Set(cacheKey, true, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

            await _notifications.SendSharedFileDownloadedNotificationAsync(
                ownerId,
                fileName,
                httpContext.Request.GetRemoteIPAddress(),
                httpContext.Request.Headers.UserAgent);
        }
    }
}
