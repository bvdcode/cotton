// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Abstractions
{
    /// <summary>
    /// Base entity for rows that belong to a specific Cotton user.
    /// </summary>
    public abstract class BaseOwnedEntity<TKey> : BaseEntity<TKey>
        where TKey : struct
    {
        /// <summary>
        /// Identifier of the user who owns this row.
        /// </summary>
        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        /// <summary>
        /// Navigation property for the owning user.
        /// </summary>
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User Owner { get; set; } = null!;
    }
}
