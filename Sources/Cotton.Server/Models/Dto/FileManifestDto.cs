// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class FileManifestDto : BaseDto<Guid>
    {
        public Guid? OwnerId { get; set; }

        public string Name { get; set; } = null!;

        public string Folder { get; set; } = null!;

        public string ContentType { get; set; } = null!;

        public long SizeBytes { get; set; }
    }
}
