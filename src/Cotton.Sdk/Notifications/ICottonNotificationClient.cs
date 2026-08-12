// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Notifications
{
    /// <summary>
    /// Provides access to notifications stored by the Cotton server.
    /// </summary>
    public interface ICottonNotificationClient
    {
        /// <summary>
        /// Gets one page of notifications ordered from newest to oldest.
        /// </summary>
        Task<CottonPagedResult<IReadOnlyList<CottonNotificationDto>>> GetNotificationsAsync(
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default);
    }
}
