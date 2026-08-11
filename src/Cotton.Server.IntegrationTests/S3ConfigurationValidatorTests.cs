// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class S3ConfigurationValidatorTests
    {
        [Test]
        public void ValidateShape_RejectsIncompleteConfigurationWithoutConnecting()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    S3ConfigurationValidator.ValidateShape(null),
                    Is.EqualTo("S3 settings must be provided."));
                Assert.That(
                    S3ConfigurationValidator.ValidateShape(CreateConfiguration(endpoint: string.Empty)),
                    Is.EqualTo("S3 endpoint URL must be provided."));
                Assert.That(
                    S3ConfigurationValidator.ValidateShape(CreateConfiguration(endpoint: "relative")),
                    Is.EqualTo("S3 endpoint URL must be an absolute HTTP or HTTPS URL."));
                Assert.That(
                    S3ConfigurationValidator.ValidateShape(CreateConfiguration(region: string.Empty)),
                    Is.EqualTo("S3 region must be provided."));
                Assert.That(
                    S3ConfigurationValidator.ValidateShape(CreateConfiguration(bucket: string.Empty)),
                    Is.EqualTo("S3 bucket must be provided."));
                Assert.That(
                    S3ConfigurationValidator.ValidateShape(CreateConfiguration(accessKey: string.Empty)),
                    Is.EqualTo("S3 access key must be provided."));
                Assert.That(
                    S3ConfigurationValidator.ValidateShape(CreateConfiguration(secretKey: string.Empty)),
                    Is.EqualTo("S3 secret key must be provided."));
            });
        }

        [Test]
        public void ValidateShape_AcceptsCompleteConfiguration()
        {
            Assert.That(S3ConfigurationValidator.ValidateShape(CreateConfiguration()), Is.Null);
        }

        private static S3Config CreateConfiguration(
            string endpoint = "https://s3.example.test",
            string region = "test-region",
            string bucket = "test-bucket",
            string accessKey = "access-key",
            string secretKey = "secret-key")
        {
            return new S3Config
            {
                Endpoint = endpoint,
                Region = region,
                Bucket = bucket,
                AccessKey = accessKey,
                SecretKey = secretKey,
            };
        }
    }
}
