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
                .Map(dest => dest.NodeId, src => src.NodeId)
                .Map(dest => dest.OwnerId, src => src.OwnerId)
                .Map(dest => dest.SizeBytes, src => src.FileManifest.SizeBytes)
                .Map(dest => dest.ContentType, src => src.FileManifest.ContentType)
                .Map(dest => dest.RequiresVideoTranscoding, src =>
                    src.FileManifest.SmallFilePreviewHash != null
                    && src.FileManifest.ContentType.StartsWith("video/")
                    && src.FileManifest.ContentType != "video/mp4"
                    && src.FileManifest.ContentType != "video/webm"
                    && src.FileManifest.ContentType != "video/ogg"
                    && src.FileManifest.ContentType != "video/quicktime")
                .Map(d => d.PreviewHashEncryptedHex, s => s.FileManifest.GetPreviewHashEncryptedHex());

            TypeAdapterConfig<User, UserDto>
                .NewConfig()
                .Map(dest => dest.AvatarHashEncryptedHex, src => src.GetAvatarHashEncryptedHex());

            _isConfigured = true;
        }
    }
}
