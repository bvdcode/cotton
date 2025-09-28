using Cotton.Crypto.Models;

namespace Cotton.Crypto.Tests
{
    public class AesGcmKeyHeaderTests
    {
        [Test]
        public void SerializeDeserialize_RoundTrip_WithCustomSizes()
        {
            AesGcmKeyHeader expectedHeader = new(123, [1, 2, 3], [4, 5, 6], [7, 8, 9], 12345);
            ReadOnlyMemory<byte> headerBytes = expectedHeader.ToBytes();
            using MemoryStream ms = new(headerBytes.ToArray());
            AesGcmKeyHeader parsedHeader = AesGcmKeyHeader.FromStream(ms, 3, 3);

            Assert.Multiple(() =>
            {
                Assert.That(parsedHeader.KeyId, Is.EqualTo(expectedHeader.KeyId));
                Assert.That(parsedHeader.Nonce, Is.EqualTo(expectedHeader.Nonce));
                Assert.That(parsedHeader.Tag, Is.EqualTo(expectedHeader.Tag));
                Assert.That(parsedHeader.EncryptedKey, Is.EqualTo(expectedHeader.EncryptedKey));
                Assert.That(parsedHeader.DataLength, Is.EqualTo(expectedHeader.DataLength));
            });
        }
    }
}