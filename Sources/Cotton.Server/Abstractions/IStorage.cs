// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

namespace Cotton.Server.Abstractions
{
    public interface IStorage
    {
        Stream GetBlobStream(string[] uids);
        Task WriteFileAsync(string uid, Stream stream, CancellationToken ct = default);
    }
}