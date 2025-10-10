namespace Cotton.Crypto.Internals
{
    internal static class FormatConstants
    {
        public const int Version = 1;
        public static ReadOnlySpan<byte> MagicBytes => "CTN1"u8;
    }
}
