// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Files
{
    public enum ResolveHlsSourceStatus
    {
        Success,

        TokenNotFound,

        FileNotFound,

        NotTranscodable,
    }
}
