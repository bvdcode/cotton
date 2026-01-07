// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class NodeFileManifestDto : BaseDto<Guid>
    {
        public Guid OwnerId { get; set; }
        public string Name { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public long SizeBytes { get; set; }
        public string? PreviewImageHash { get; set; }
    }
}
