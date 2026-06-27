// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Runtime switches for database integrity validation.
    /// </summary>
    public record DatabaseIntegrityRuntimeOptions(
        bool ReadValidationEnabled,
        bool SaveOriginalStateValidationEnabled);
}
