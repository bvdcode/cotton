// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Server;
using EasyExtensions.Mediator;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class VectorIndexBuildTestHandler(Func<string?> result) : IRequestHandler<BuildVectorIndexRequest, string?>
    {
        public Task<string?> Handle(BuildVectorIndexRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(result());
        }
    }
}
