// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Abstractions
{
    /// <summary>Records sync feed rows in the current database unit of work.</summary>
    public interface ISyncChangeRecorder
    {
        /// <summary>Stages a sync feed row for the caller's next database save.</summary>
        void Stage(SyncChange change);
    }
}
