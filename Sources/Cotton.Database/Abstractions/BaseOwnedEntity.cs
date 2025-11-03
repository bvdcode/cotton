// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Database.Models;
using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Database.Abstractions
{
    public abstract class BaseOwnedEntity : BaseEntity<Guid>
    {
        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        public virtual User Owner { get; set; } = null!;
    }
}
