// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Runtime switch for database integrity enforcement.
    /// </summary>
    public record DatabaseIntegrityRuntimeOptions(bool EnforcementEnabled);
}
