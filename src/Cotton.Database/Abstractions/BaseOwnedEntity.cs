// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Abstractions
{
    public abstract class BaseOwnedEntity<TKey> : BaseEntity<TKey>
        where TKey : struct
    {
        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User Owner { get; set; } = null!;
    }
}
