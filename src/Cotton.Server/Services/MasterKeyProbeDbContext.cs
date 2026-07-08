// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Minimal raw database context used before the normal encrypted EF converters are available.
    /// </summary>
    internal class MasterKeyProbeDbContext(DbContextOptions<MasterKeyProbeDbContext> options) : DbContext(options)
    {
        /// <summary>
        /// User rows inspected for master-key evidence.
        /// </summary>
        public DbSet<MasterKeyProbeUser> Users => Set<MasterKeyProbeUser>();

        /// <summary>
        /// Node rows used to detect existing Cotton data.
        /// </summary>
        public DbSet<MasterKeyProbeNode> Nodes => Set<MasterKeyProbeNode>();

        /// <summary>
        /// File manifest rows inspected for master-key evidence.
        /// </summary>
        public DbSet<MasterKeyProbeFileManifest> FileManifests => Set<MasterKeyProbeFileManifest>();

        /// <summary>
        /// Chunk rows inspected for storage evidence.
        /// </summary>
        public DbSet<MasterKeyProbeChunk> Chunks => Set<MasterKeyProbeChunk>();

        /// <summary>
        /// Server settings rows inspected for encrypted configuration and startup storage selection.
        /// </summary>
        public DbSet<MasterKeyProbeServerSettings> ServerSettings => Set<MasterKeyProbeServerSettings>();

        /// <summary>
        /// OIDC provider rows inspected for encrypted client-secret evidence.
        /// </summary>
        public DbSet<MasterKeyProbeOidcProvider> OidcProviders => Set<MasterKeyProbeOidcProvider>();

        /// <summary>
        /// OIDC login-state rows inspected for short-lived encrypted evidence.
        /// </summary>
        public DbSet<MasterKeyProbeOidcLoginState> OidcLoginStates => Set<MasterKeyProbeOidcLoginState>();
    }
}
