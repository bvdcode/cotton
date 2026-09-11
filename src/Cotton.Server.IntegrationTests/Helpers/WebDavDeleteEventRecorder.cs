// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class WebDavDeleteEventRecorder
    {
        public int FileDeletedCount { get; set; }
        public Guid? FileDeletedNodeFileId { get; set; }
        public Guid? FileDeletedParentNodeId { get; set; }
        public int NodeDeletedCount { get; set; }
        public Guid? NodeDeletedNodeId { get; set; }
        public Guid? NodeDeletedParentNodeId { get; set; }
    }
}
