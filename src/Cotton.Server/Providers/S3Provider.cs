using Amazon.Runtime;
using Amazon.S3;
using Cotton.Storage.Abstractions;
using EasyExtensions.Abstractions;
using EasyExtensions.Extensions;

namespace Cotton.Server.Providers
{
    public class S3Provider(
        IStreamCipher _crypto,
        SettingsProvider _settingsProvider) : IS3Provider
    {
        private IAmazonS3? _s3Client;
        private string? _bucketName;

        public string GetBucketName()
        {
            if (!string.IsNullOrEmpty(_bucketName))
            {
                return _bucketName;
            }
            string? result = _settingsProvider.GetServerSettings().S3BucketName;
            if (string.IsNullOrEmpty(result))
            {
                throw new InvalidOperationException("S3 bucket name is not configured.");
            }
            _bucketName = result;
            return result;
        }

        public IAmazonS3 GetS3Client()
        {
            if (_s3Client != null)
            {
                return _s3Client;
            }
            var settings = _settingsProvider.GetServerSettings();
            ArgumentNullException.ThrowIfNull(settings.S3EndpointUrl, nameof(settings.S3EndpointUrl));
            ArgumentNullException.ThrowIfNull(settings.S3AccessKeyId, nameof(settings.S3AccessKeyId));
            ArgumentNullException.ThrowIfNull(settings.S3SecretAccessKeyEncrypted, nameof(settings.S3SecretAccessKeyEncrypted));
            ArgumentNullException.ThrowIfNull(settings.S3BucketName, nameof(settings.S3BucketName));
            ArgumentNullException.ThrowIfNull(settings.S3Region, nameof(settings.S3Region));
            var config = new AmazonS3Config
            {
                ServiceURL = settings.S3EndpointUrl,
                ForcePathStyle = true,
                AuthenticationRegion = settings.S3Region,
                UseHttp = false,
                MaxErrorRetry = 3,
                Timeout = TimeSpan.FromMinutes(5),
            };
            string? decryptedSecretKey = _settingsProvider.DecryptValue(settings.S3SecretAccessKeyEncrypted);
            if (string.IsNullOrEmpty(decryptedSecretKey))
            {
                throw new InvalidOperationException("Failed to decrypt S3 secret access key.");
            }
            var credentials = new BasicAWSCredentials(settings.S3AccessKeyId, decryptedSecretKey);
            _s3Client = new AmazonS3Client(credentials, config);
            return _s3Client;
        }
    }
}
