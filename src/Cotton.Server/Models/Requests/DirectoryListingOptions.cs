// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Cotton.Server.Models.Requests
{
    public class DirectoryListingOptions
    {
        public DirectoryField SortBy { get; set; }
        public bool Descending { get; set; }
        public DirectoryField FilterBy { get; set; }
        public DirectoryFilterOperator FilterOperator { get; set; }
        [MaxLength(512)]
        public string? FilterValue { get; set; }
        [MaxLength(100)]
        public string[] FilterValues { get; set; } = [];
        public bool IsDefault => SortBy == DirectoryField.Name
            && !Descending && FilterOperator == DirectoryFilterOperator.None;
    }
}
