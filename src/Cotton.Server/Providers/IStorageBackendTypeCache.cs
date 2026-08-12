// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;

namespace Cotton.Server.Providers
{
    public interface IStorageBackendTypeCache
    {
        StorageType GetOrAdd(Func<StorageType> resolve);

        void Reset();
    }
}
