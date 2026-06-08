// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Files;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Mapster;

namespace Cotton.Server.Mappings
{
    /// <summary>
    /// Describes mapster configuration.
    /// </summary>
    public static class MapsterConfig
    {
        private static bool _isConfigured;

        /// <summary>
        /// Registers Mapster mappings used by the API layer.
        /// </summary>
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
                .Map(dest => dest.FileManifestId, src => src.FileManifestId)
                .Map(dest => dest.OriginalNodeFileId, src => src.OriginalNodeFileId)
                .Map(dest => dest.OwnerId, src => src.OwnerId)
                .Map(dest => dest.SizeBytes, src => src.FileManifest.SizeBytes)
                .Map(dest => dest.ContentType, src => src.FileManifest.ContentType)
                .Map(dest => dest.ContentHash, src => Hasher.ToHexStringHash(src.FileManifest.ProposedContentHash))
                .Map(dest => dest.ETag, src => FileETags.GetContentETag(src.FileManifest))
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
