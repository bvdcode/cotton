// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

namespace Cotton.Server.Services.DatabaseIntegrity;

public interface IDatabaseIntegrityKeyProvider
{
    HMACSHA256 CreateHmac();
}
