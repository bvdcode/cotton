// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public record FileDownloadToken(Guid UserId, Guid NodeFileId, DateTimeOffset ExpiresAt)
    {
        public static string BuildUrl(Guid nodeFileId, string token) =>
            $"{Routes.V1.Files}/{nodeFileId}/download?downloadToken={token}";
    }
}
