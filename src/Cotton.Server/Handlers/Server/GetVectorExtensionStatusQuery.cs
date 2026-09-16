// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Server
{
    public class GetVectorExtensionStatusQuery : IRequest<VectorExtensionStatusDto>
    {
    }

    public class GetVectorExtensionStatusQueryHandler(CottonDbContext dbContext)
        : IRequestHandler<GetVectorExtensionStatusQuery, VectorExtensionStatusDto>
    {
        public async Task<VectorExtensionStatusDto> Handle(
            GetVectorExtensionStatusQuery request,
            CancellationToken cancellationToken)
        {
            const string extensionStatusSql = """
                SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_extension WHERE extname = 'vector') AS "Value"
                """;
            bool extensionEnabled = await dbContext.Database.SqlQueryRaw<bool>(extensionStatusSql)
                .SingleAsync(cancellationToken);
            long vectorCount = await dbContext.FileEmbeddings.LongCountAsync(cancellationToken);

            return new VectorExtensionStatusDto
            {
                ExtensionEnabled = extensionEnabled,
                VectorCount = vectorCount
            };
        }
    }
}
