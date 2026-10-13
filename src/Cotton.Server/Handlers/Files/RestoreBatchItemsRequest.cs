// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Server.Handlers.Nodes;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Files
{
    public record RestoreBatchItemsRequest(Guid UserId, IReadOnlyList<BatchItemRequestDto> Items)
        : IRequest<IReadOnlyList<BatchItemResultDto>>;

    public class RestoreBatchItemsRequestHandler(
        IServiceScopeFactory _scopeFactory,
        ILogger<RestoreBatchItemsRequestHandler> _logger)
        : IRequestHandler<RestoreBatchItemsRequest, IReadOnlyList<BatchItemResultDto>>
    {
        public async Task<IReadOnlyList<BatchItemResultDto>> Handle(
            RestoreBatchItemsRequest request,
            CancellationToken ct)
        {
            List<BatchItemResultDto> results = new List<BatchItemResultDto>(request.Items.Count);
            foreach (BatchItemRequestDto item in request.Items)
            {
                ct.ThrowIfCancellationRequested();
                BatchItemResultDto result = new BatchItemResultDto { Id = item.Id, Kind = item.Kind };
                try
                {
                    await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                    IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    result.RestoreOutcome = item.Kind switch
                    {
                        BatchItemKind.Folder => await mediator.Send(new RestoreNodeQuery(
                            request.UserId, item.Id, item.CreateMissingParents, item.Overwrite), ct),
                        BatchItemKind.File => await mediator.Send(new RestoreFileQuery(
                            request.UserId, item.Id, item.CreateMissingParents, item.Overwrite), ct),
                        _ => throw new ArgumentOutOfRangeException(nameof(item.Kind)),
                    };
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restore {Kind} {ItemId} for user {UserId}.",
                        item.Kind, item.Id, request.UserId);
                    result.Failed = true;
                }

                results.Add(result);
            }

            return results;
        }
    }
}
