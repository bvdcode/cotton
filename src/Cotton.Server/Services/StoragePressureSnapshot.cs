// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Localization;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.Configuration;
using Cotton.Storage.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Represents storage pressure snapshot.
    /// </summary>
    public record StoragePressureSnapshot(
        StorageCapacitySnapshot Capacity,
        long IncomingBytes,
        long RequiredFreeBytes,
        long ProjectedAvailableBytes);
}
