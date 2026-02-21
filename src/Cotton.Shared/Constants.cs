namespace Cotton
{
    /// <summary>
    /// Provides application-wide constant values for path separators and product names.
    /// </summary>
    /// <remarks>These constants are intended to promote consistency and avoid the use of magic values
    /// throughout the application. Use these values when constructing file paths or displaying product information to
    /// ensure uniformity across different components.</remarks>
    public static class Constants
    {
        /// <summary>
        /// Represents the default character used to separate paths in a file system.
        /// </summary>
        /// <remarks>This constant is typically used in file path manipulations to ensure compatibility
        /// across different operating systems.</remarks>
        public const char DefaultPathSeparator = '/';

        /// <summary>
        /// Gets the short name of the product, which is "Cotton".
        /// </summary>
        public const string ShortProductName = "Cotton";

        /// <summary>
        /// Gets the name of the product, which is "Cotton Cloud".
        /// </summary>
        public const string ProductName = "Cotton Cloud";
    }
}
