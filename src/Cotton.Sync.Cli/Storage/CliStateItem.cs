// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Sync.Cli.Storage;

/// <summary>
/// Represents one persisted CLI state value.
/// </summary>
[Table("cli_state")]
public sealed class CliStateItem
{
    /// <summary>
    /// Gets or sets the state key.
    /// </summary>
    [Key]
    [MaxLength(128)]
    [Column("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the state value.
    /// </summary>
    [Required]
    [Column("value")]
    public string Value { get; set; } = string.Empty;
}
