// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public record ArchiveDownloadTicket(
        Guid UserId,
        string FileName,
        Guid[] FileIds,
        Guid[] NodeIds,
        bool EnforcePublicShareLimits,
        string? ShareToken = null);
}
