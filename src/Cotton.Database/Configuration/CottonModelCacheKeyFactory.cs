// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cotton.Database.Configuration
{
    internal class CottonModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            if (context is not CottonDbContext cottonDbContext)
            {
                throw new ArgumentException(
                    $"{nameof(CottonModelCacheKeyFactory)} only supports {nameof(CottonDbContext)}.",
                    nameof(context));
            }

            return new CottonModelCacheKey(
                cottonDbContext.GetType(),
                cottonDbContext.DatabaseFieldProtector,
                designTime);
        }
    }
}
