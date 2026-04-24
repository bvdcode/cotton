using System;

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
        /// Gets the short name of the product, which is "cotton".
        /// </summary>
        public const string ShortProductName = "cotton";

        /// <summary>
        /// Gets the name of the product, which is "Cotton Cloud".
        /// </summary>
        public const string ProductName = "Cotton Cloud";

        /// <summary>
        /// Specifies the delay, in minutes, before an administrator account is automatically created.
        /// </summary>
        /// <remarks>Adjust this value to control the timing of automatic admin account creation.
        /// Modifying the delay can help coordinate account provisioning in different deployment or initialization
        /// scenarios.</remarks>
        public const int AdminAutocreateMinutesDelay = 5;

        /// <summary>
        /// Gets the environment variable key that marks the instance as public/demo.
        /// </summary>
        public const string PublicInstanceEnvironmentVariable = "COTTON_PUBLIC_INSTANCE";

        /// <summary>
        /// Indicates whether the current process is running as a public/demo instance.
        /// </summary>
        public static readonly bool IsPublicInstance =
            bool.TryParse(Environment.GetEnvironmentVariable(PublicInstanceEnvironmentVariable), out bool isPublic)
            && isPublic;
    }
}
