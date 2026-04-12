using System.Text;

namespace Cotton.Server.Services
{
    public class DatabaseBackupKeyProvider(CottonEncryptionSettings encryptionSettings)
    {
        public const string ManifestPointerLogicalKey = "database.ctn";

        public string GetScopedPointerStorageKey()
        {
            string scopedLogicalKey = $"{ManifestPointerLogicalKey}:{encryptionSettings.MasterEncryptionKey}";
            return Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(scopedLogicalKey)));
        }
    }
}
