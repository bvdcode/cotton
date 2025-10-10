using System.Buffers.Binary;
using Cotton.Crypto.Internals;

namespace Cotton.Crypto.Tests;

public class FormatInvariantTests
{
    [Test]
    public void ComposeNonce_RoundTrip_NoCollisions_ForFirstMillion()
    {
        uint prefix = 0xA1B2C3D4;
        var seen = new HashSet<ulong>(1_000_000);
        Span<byte> nonce = stackalloc byte[AesGcmStreamCipher.NonceSize];

        for (long i = 0; i < 1_000_000; i++)
        {
            AesGcmStreamFormat.ComposeNonce(nonce, prefix, i);
            Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(nonce[..4]), Is.EqualTo(prefix));
            ulong ctr = BinaryPrimitives.ReadUInt64LittleEndian(nonce[4..]);
            Assert.That(seen.Add(ctr), Is.True, "Duplicate counter detected at i=" + i);
            Assert.That(ctr, Is.EqualTo((ulong)i));
        }
    }

    [Test]
    public void ComposeNonce_Overflow_Throws()
    {
        uint prefix = 0x01020304;
        Span<byte> nonce = stackalloc byte[AesGcmStreamCipher.NonceSize];
        bool thrown = false;
        try
        {
            AesGcmStreamFormat.ComposeNonce(nonce, prefix, -1L);
        }
        catch (InvalidOperationException)
        {
            thrown = true;
        }
        Assert.That(thrown, Is.True);
    }

    [Test]
    public void AadPrefix_IsConstant_MutableFieldsChangeOnly()
    {
        int keyId = 123;
        Span<byte> aad = stackalloc byte[32];
        AesGcmStreamFormat.InitAadPrefix(aad, keyId);
        byte[] prefix = aad.ToArray();

        // mutate with several values and assert prefix unchanged and fields set
        long[] indices = [0L, 1L, 123456789L];
        long[] lengths = [0L, 1L, 42L, 8_388_608L];
        foreach (var idx in indices)
        {
            foreach (var len in lengths)
            {
                AesGcmStreamFormat.FillAadMutable(aad, idx, len);
                // First 12 bytes: magic(4) + version(4) + keyId(4)
                Assert.That(aad[..12].ToArray(), Is.EqualTo(prefix[..12]));
                Assert.That(BinaryPrimitives.ReadInt64LittleEndian(aad.Slice(12, 8)), Is.EqualTo(idx));
                Assert.That(BinaryPrimitives.ReadInt64LittleEndian(aad.Slice(20, 8)), Is.EqualTo(len));
                Assert.That(BinaryPrimitives.ReadInt32LittleEndian(aad.Slice(28, 4)), Is.EqualTo(0));
            }
        }
    }
}
