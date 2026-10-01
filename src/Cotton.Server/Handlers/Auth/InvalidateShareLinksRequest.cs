// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth
{
    public record InvalidateShareLinksRequest(Guid UserId) : IRequest;

    public class InvalidateShareLinksRequestHandler(
        DownloadTokenExpirationService expirations) : IRequestHandler<InvalidateShareLinksRequest>
    {
        public Task Handle(InvalidateShareLinksRequest request, CancellationToken cancellationToken)
        {
            return expirations.ExpireActiveTokensCreatedByUserAsync(
                request.UserId, DateTime.UtcNow, cancellationToken);
        }
    }
}
