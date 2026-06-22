// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Text;
using System.Xml;

namespace Cotton.Server.Services.WebDav
{
    /// <summary>
    /// Represents WebDAV quota properties exposed through PROPFIND.
    /// </summary>
    /// <remarks>
    /// quota-used-bytes is always known from Cotton's logical file references. quota-available-bytes is only emitted when
    /// the instance has a configured user quota; without a quota, Cotton does not pretend to know backend free space.
    /// </remarks>
    public record WebDavQuota(
        long UsedBytes,
        long? AvailableBytes);
}
