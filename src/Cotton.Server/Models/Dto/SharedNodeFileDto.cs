// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class SharedNodeFileDto : BaseDto<Guid>
    {
        public Guid NodeId { get; set; }
        public string Name { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public long SizeBytes { get; set; }
        public string? PreviewHashEncryptedHex { get; set; }
    }
}
