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

        public string GetBucketName()
        {
            string? result = _settingsProvider.GetServerSettings().S3BucketName;
            if (string.IsNullOrEmpty(result))
            {
                throw new InvalidOperationException("S3 bucket name is not configured.");
            }
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
            byte[] encryptedSecret = Convert.FromBase64String(settings.S3SecretAccessKeyEncrypted);
            string decryptedSecretKey = _crypto.Decrypt(encryptedSecret);
            var credentials = new BasicAWSCredentials(settings.S3AccessKeyId, decryptedSecretKey);
            _s3Client = new AmazonS3Client(credentials, config);
            return _s3Client;
        }
    }
}
