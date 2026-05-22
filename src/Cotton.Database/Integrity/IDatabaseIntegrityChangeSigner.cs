// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;

namespace Cotton.Database.Integrity;

/// <summary>
/// Signs pending protected entity changes before EF persists them.
/// </summary>
public interface IDatabaseIntegrityChangeSigner
{
    /// <summary>Signs all pending Added and Modified protected entities in the supplied context.</summary>
    void SignPendingChanges(DbContext dbContext);
}
