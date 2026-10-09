// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Collections.Generic;

namespace Cotton.Files
{
    public class BatchItemsRequestDto
    {
        public List<BatchItemRequestDto> Items { get; set; } = [];

        public bool SkipTrash { get; set; }
    }
}
