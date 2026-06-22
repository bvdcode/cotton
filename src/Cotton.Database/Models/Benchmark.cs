// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>
    /// Stores one local performance benchmark result for server diagnostics.
    /// </summary>
    [Table("benchmarks")]
    public class Benchmark : BaseEntity<Guid>
    {
        /// <summary>
        /// Domain type discriminator for this row.
        /// </summary>
        [Column("type")]
        public BenchmarkType Type { get; set; }

        /// <summary>
        /// Human-readable name displayed to users.
        /// </summary>
        [Column("name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Measured benchmark value.
        /// </summary>
        [Column("value")]
        public long Value { get; set; }

        /// <summary>
        /// Units used by the benchmark value.
        /// </summary>
        [Column("units")]
        public string Units { get; set; } = null!;

        /// <summary>
        /// Elapsed time measured by the benchmark run.
        /// </summary>
        [Column("elapsed")]
        public TimeSpan Elapsed { get; set; }
    }
}
