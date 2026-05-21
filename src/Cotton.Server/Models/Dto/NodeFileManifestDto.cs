// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class NodeFileManifestDto : BaseDto<Guid>
    {
        public Guid NodeId { get; set; }
        public Guid OwnerId { get; set; }
        public string Name { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public long SizeBytes { get; set; }

        private Dictionary<string, string> _metadata = [];
        public Dictionary<string, string> Metadata
        {
            get => _metadata;
            set => _metadata = value ?? [];
        }

        public bool RequiresVideoTranscoding { get; set; }

        public string? PreviewHashEncryptedHex { get; set; }
    }
}
