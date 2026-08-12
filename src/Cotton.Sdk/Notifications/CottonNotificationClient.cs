// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using Cotton.Sdk.Internal;

namespace Cotton.Sdk.Notifications
{
    /// <summary>
    /// Provides access to notifications stored by the Cotton server.
    /// </summary>
    public class CottonNotificationClient : ICottonNotificationClient
    {
        private readonly CottonHttpTransport _transport;

        internal CottonNotificationClient(CottonHttpTransport transport)
        {
            _transport = transport;
        }

        /// <inheritdoc />
        public Task<CottonPagedResult<IReadOnlyList<CottonNotificationDto>>> GetNotificationsAsync(
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

            string path = $"{Routes.V1.Notifications}?page={page}&pageSize={pageSize}";
            return _transport.SendPagedJsonAsync<IReadOnlyList<CottonNotificationDto>>(
                HttpMethod.Get,
                path,
                cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public Task<CottonNotificationBatchDto> GetNotificationBatchAsync(
            CottonNotificationCursorDto? cursor = null,
            int detailLimit = 50,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(detailLimit);

            string path = $"{Routes.V1.Notifications}/batch?detailLimit={detailLimit}";
            if (cursor is not null)
            {
                string createdAt = Uri.EscapeDataString(
                    cursor.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
                path += $"&cursorCreatedAt={createdAt}&cursorNotificationId={cursor.NotificationId:D}";
            }

            return _transport.SendJsonAsync<CottonNotificationBatchDto>(
                HttpMethod.Get,
                path,
                cancellationToken: cancellationToken);
        }
    }
}
