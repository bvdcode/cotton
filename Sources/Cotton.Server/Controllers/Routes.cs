﻿namespace Cotton.Server.Controllers
{
    public static class Routes
    {
        public const string Root = "/api";
        public const string Version = "/v1";
        public const string Base = Root + Version;

        public const string Files = Base + "/files";
        public const string Chunks = Base + "/chunks";
        public const string Layouts = Base + "/layouts";
    }
}
