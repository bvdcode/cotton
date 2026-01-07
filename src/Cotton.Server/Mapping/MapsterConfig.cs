// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Mapster;

namespace Cotton.Server.Mapping
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
                .Map(dest => dest.PreviewImageHash, src => src.FileManifest.PreviewImageHash == null ? null : Hasher.ToHexStringHash(src.FileManifest.PreviewImageHash))
                .Map(dest => dest.ContentType, src => src.FileManifest.ContentType)
                .Map(dest => dest.SizeBytes, src => src.FileManifest.SizeBytes);

            _isConfigured = true;
        }
    }
}
