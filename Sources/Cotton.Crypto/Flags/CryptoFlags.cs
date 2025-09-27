namespace Cotton.Crypto.Flags
{
    [Flags]
    public enum CryptoFlags : int
    {
        None = 0,
        HasFooter = 1,
        Reserved1 = 2,
        Reserved2 = 4
    }
}