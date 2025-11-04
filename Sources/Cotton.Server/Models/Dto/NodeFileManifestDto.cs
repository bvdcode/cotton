// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Database.Models;
using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class NodeFileManifestDto : BaseDto<Guid>
    {
        public Guid OwnerId { get; set; }
        public string Name { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public long SizeBytes { get; set; }

        internal void ReadMetadataFromManifest(FileManifest newFile)
        {
            ContentType = newFile.ContentType;
            SizeBytes = newFile.SizeBytes;
        }
    }
}
