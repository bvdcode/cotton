// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Mapster;

namespace Cotton.Server.Mappings
{
    public static class MapsterConfig
    {
        private static bool _isConfigured;

        public static void Register()
        {
            if (_isConfigured)
            {
                return;
            }

            TypeAdapterConfig<NodeFile, NodeFileManifestDto>
                .NewConfig()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Name, src => src.Name)
                .Map(dest => dest.OwnerId, src => src.OwnerId)
                .Map(dest => dest.SizeBytes, src => src.FileManifest.SizeBytes)
                .Map(dest => dest.ContentType, src => src.FileManifest.ContentType)
                .Map(dest => dest.EncryptedFilePreviewHashHex,
                    src => src.FileManifest.EncryptedFilePreviewHash == null ? null : Convert.ToHexString(src.FileManifest.EncryptedFilePreviewHash));

            _isConfigured = true;
        }
    }
}
