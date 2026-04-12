using Cotton.Server.Models.DatabaseBackup;

namespace Cotton.Server.Abstractions
{
    public interface IDatabaseBackupManifestService
    {
        Task<ResolvedBackupManifest?> TryGetLatestManifestAsync(CancellationToken cancellationToken = default);
    }
}
