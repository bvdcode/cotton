namespace Cotton
{
    /// <summary>
    /// Defines API route constants used throughout the Cotton Cloud application.
    /// </summary>
    public static class Routes
    {
        /// <summary>
        /// Defines API v1 route constants.
        /// </summary>
        public static class V1
        {
            /// <summary>
            /// Base path for all API v1 endpoints.
            /// </summary>
            public const string Base = "/api/v1";

            /// <summary>
            /// Authentication endpoint path.
            /// </summary>
            public const string Auth = Base + "/auth";

            /// <summary>
            /// Users endpoint path.
            /// </summary>
            public const string Users = Base + "/users";

            /// <summary>
            /// Files endpoint path.
            /// </summary>
            public const string Files = Base + "/files";

            /// <summary>
            /// Server endpoint path.
            /// </summary>
            public const string Server = Base + "/server";

            /// <summary>
            /// Chunks endpoint path.
            /// </summary>
            public const string Chunks = Base + "/chunks";

            /// <summary>
            /// Layouts endpoint path.
            /// </summary>
            public const string Layouts = Base + "/layouts";

            /// <summary>
            /// Preview endpoint path.
            /// </summary>
            public const string Previews = Base + "/preview";

            /// <summary>
            /// Event Hub endpoint path.
            /// </summary>
            public const string EventHub = Base + "/hub/events";

            /// <summary>
            /// Notifications endpoint path.
            /// </summary>
            public const string Notifications = Base + "/notifications";
        }
    }
}
