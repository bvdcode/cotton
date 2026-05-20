// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class UserStorageQuotaDto
    {
        public long UsedBytes { get; set; }
        public long? QuotaBytes { get; set; }
        public long? AvailableBytes { get; set; }
    }
}
