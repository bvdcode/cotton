// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Integrity;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Leaves protected row changes unsigned while database integrity enforcement is disabled.
    /// </summary>
    public class DisabledDatabaseIntegrityChangeSigner : IDatabaseIntegrityChangeSigner
    {
        /// <inheritdoc />
        public void SignPendingChanges(DbContext dbContext)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
        }
    }
}
