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
    internal class StoragePressureReservation(
        StoragePressureGuard? owner,
        long bytes) : IDisposable
    {
        public static StoragePressureReservation None => new(null, 0);

        private readonly StoragePressureGuard? _owner = owner;
        private readonly long _bytes = Math.Max(0, bytes);
        private bool _committed;
        private bool _disposed;

        public void Commit()
        {
            _committed = true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!_committed)
            {
                _owner?.ReleaseReservation(_bytes);
            }
        }
    }
}
