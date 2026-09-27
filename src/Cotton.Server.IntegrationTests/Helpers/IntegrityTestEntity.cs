// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests.Helpers
{
    public record IntegrityTestEntity
    {
        public Guid Id { get; init; }
        public Guid? OwnerId { get; init; }
        public string? Name { get; init; }
        public long SizeBytes { get; init; }
        public bool IsEnabled { get; init; }
        public DateTime? SeenAt { get; init; }
        public string[]? Transports { get; init; }
        public Dictionary<string, string>? Metadata { get; init; }
    }
}
