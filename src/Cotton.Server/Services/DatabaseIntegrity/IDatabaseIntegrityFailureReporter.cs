// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

public interface IDatabaseIntegrityFailureReporter
{
    void Report(DatabaseIntegrityFailure failure);
}
