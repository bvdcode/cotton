// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;

namespace Cotton.Server.Services
{
    /// <summary>Stages durable sync feed rows without performing database I/O by itself.</summary>
    public class SyncChangeRecorder(CottonDbContext _dbContext) : ISyncChangeRecorder
    {
        /// <inheritdoc />
        public void Stage(SyncChange change)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(change.Kind, SyncChangeKind.Unknown);
            _dbContext.SyncChanges.Add(change);
        }
    }
}
