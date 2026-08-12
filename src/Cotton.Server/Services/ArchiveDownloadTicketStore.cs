// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    public class ArchiveDownloadTicketStore(IMemoryCache _cache)
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
        private const string CacheKeyPrefix = "archive-download:";

        public string Store(ArchiveDownloadTicket ticket)
        {
            ArgumentNullException.ThrowIfNull(ticket);

            string token = CreateToken();
            _cache.Set(CacheKeyPrefix + token, ticket, Lifetime);
            return token;
        }

        public bool TryGet(string token, out ArchiveDownloadTicket ticket)
        {
            ticket = null!;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return _cache.TryGetValue(CacheKeyPrefix + token, out ticket!);
        }

        private static string CreateToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        }
    }
}
