using System.Text;
using Cotton.Crypto.Hashers;
using Cotton.Crypto.Abstractions;

namespace Cotton.Crypto.Tests
{
    [TestFixture]
    public class Sha256HasherTests
    {
        private static string Hex(byte[] b)
        {
            var c = new char[b.Length * 2];
            const string h = "0123456789abcdef";
            for (int i = 0; i < b.Length; i++) { c[2*i] = h[b[i]>>4]; c[2*i+1] = h[b[i]&0xF]; }
            return new string(c);
        }

        [Test]
        public void Hash_Empty()
        {
            Sha256Hasher hasher = new();
            var got = hasher.ComputeHash([]);
            Assert.That(Hex(got), Is.EqualTo("e3b0c44298fc1c149afbf4c8996fb924" +
                                              "27ae41e4649b934ca495991b7852b855"));
        }

        [Test]
        public void Hash_KnownVector_abc()
        {
            Sha256Hasher hasher = new();
            var got = hasher.ComputeHash(Encoding.ASCII.GetBytes("abc"));
            Assert.That(Hex(got), Is.EqualTo("ba7816bf8f01cfea414140de5dae2223" +
                                              "b00361a396177a9cb410ff61f20015ad"));
        }

        [Test]
        public void Hash_MultipleCalls_StateNotShared()
        {
            Sha256Hasher hasher = new();
            var a1 = hasher.ComputeHash(Encoding.ASCII.GetBytes("a"));
            var a2 = hasher.ComputeHash(Encoding.ASCII.GetBytes("a"));
            Assert.That(a1, Is.EqualTo(a2));
        }

        [Test]
        public void Hash_Stream_Equals_Span()
        {
            Sha256Hasher hasher = new();
            byte[] data = [.. Enumerable.Range(0, 10_000).Select(i => (byte)(i * 31))];
            var spanHash = hasher.ComputeHash(data);
            using var ms = new MemoryStream(data, writable:false);
            var streamHash = hasher.ComputeHash(ms);
            Assert.That(streamHash, Is.EqualTo(spanHash));
        }

        [Test]
        public void Hash_Stream_ZeroLength()
        {
            Sha256Hasher hasher = new();
            using var ms = new MemoryStream([]);
            Span<byte> dest = stackalloc byte[32];
            hasher.ComputeHash(ms, dest);
            var copy = dest.ToArray();
            Assert.That(Hex(copy), Is.EqualTo("e3b0c44298fc1c149afbf4c8996fb924" +
                                                "27ae41e4649b934ca495991b7852b855"));
        }

        private static void CallSpanWithEmptyDest(Sha256Hasher hasher)
        {
            hasher.ComputeHash([], []); // should throw
        }

        private static void CallStreamWithSmallDest(Sha256Hasher hasher)
        {
            using var ms = new MemoryStream();
            Span<byte> small = stackalloc byte[16];
            hasher.ComputeHash(ms, small); // should throw
        }

        private static void CallStreamNull(Sha256Hasher hasher)
        {
            hasher.ComputeHash((Stream)null!); // should throw
        }

        private static void CallStreamNullDest(Sha256Hasher hasher)
        {
            Span<byte> dest = stackalloc byte[32];
            hasher.ComputeHash((Stream)null!, dest); // should throw
        }

        [Test]
        public void Hash_Destination_WrongSize_Throws()
        {
            Sha256Hasher hasher = new();
            Assert.Throws<ArgumentException>(() => CallSpanWithEmptyDest(hasher));
            Assert.Throws<ArgumentException>(() => CallStreamWithSmallDest(hasher));
        }

        [Test]
        public void Hash_NullStream_Throws()
        {
            Sha256Hasher hasher = new();
            Assert.Throws<ArgumentNullException>(() => CallStreamNull(hasher));
            Assert.Throws<ArgumentNullException>(() => CallStreamNullDest(hasher));
        }
    }
}
