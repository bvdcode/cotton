// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Cotton.Storage.Helpers;

namespace Cotton.Storage.Tests.Helpers
{
    [TestFixture]
    public class S3CompatibilityFactoryTests
    {
        [Test]
        public void BuildConfig_UsesS3CompatibleDefaults()
        {
            AmazonS3Config config = S3CompatibilityFactory.BuildConfig(
                "https://s3.example.test",
                "auto",
                timeout: TimeSpan.FromSeconds(30),
                maxErrorRetry: 5);

            Assert.Multiple(() =>
            {
                Assert.That(config.ServiceURL, Is.EqualTo("https://s3.example.test/"));
                Assert.That(config.AuthenticationRegion, Is.EqualTo("auto"));
                Assert.That(config.ForcePathStyle, Is.True);
                Assert.That(config.UseHttp, Is.False);
                Assert.That(config.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
                Assert.That(config.MaxErrorRetry, Is.EqualTo(5));
                Assert.That(config.RequestChecksumCalculation, Is.EqualTo(RequestChecksumCalculation.WHEN_REQUIRED));
                Assert.That(config.ResponseChecksumValidation, Is.EqualTo(ResponseChecksumValidation.WHEN_REQUIRED));
            });
        }

        [Test]
        public void WithFileBodyCompatibility_DisablesChunkEncodingOnly()
        {
            PutObjectRequest request = new PutObjectRequest().WithFileBodyCompatibility();

            Assert.Multiple(() =>
            {
                Assert.That(request.UseChunkEncoding, Is.False);
                Assert.That(request.DisablePayloadSigning, Is.Null);
            });
        }

        [Test]
        public void WithInMemoryBodyCompatibility_DisablesChunkEncodingAndPayloadSigning()
        {
            PutObjectRequest request = new PutObjectRequest().WithInMemoryBodyCompatibility();

            Assert.Multiple(() =>
            {
                Assert.That(request.UseChunkEncoding, Is.False);
                Assert.That(request.DisablePayloadSigning, Is.True);
            });
        }
    }
}
