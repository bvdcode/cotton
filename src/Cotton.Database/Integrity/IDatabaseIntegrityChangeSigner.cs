// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;

namespace Cotton.Database.Integrity;

public interface IDatabaseIntegrityChangeSigner
{
    void SignPendingChanges(DbContext dbContext);
}
