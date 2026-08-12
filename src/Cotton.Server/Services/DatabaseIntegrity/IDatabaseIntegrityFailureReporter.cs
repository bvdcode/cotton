// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Reports database-integrity failures after a security-sensitive read rejects a row.
    /// </summary>
    public interface IDatabaseIntegrityFailureReporter
    {
        void Report(DatabaseIntegrityFailure failure);
    }
}
