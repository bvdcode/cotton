// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Runtime.CompilerServices;

namespace Cotton.Database.Configuration
{
    internal class CottonModelCacheKey : IEquatable<CottonModelCacheKey>
    {
        private readonly Type _contextType;
        private readonly IDatabaseFieldProtector? _databaseFieldProtector;
        private readonly bool _designTime;

        public CottonModelCacheKey(
            Type contextType,
            IDatabaseFieldProtector? databaseFieldProtector,
            bool designTime)
        {
            _contextType = contextType;
            _databaseFieldProtector = databaseFieldProtector;
            _designTime = designTime;
        }

        public bool Equals(CottonModelCacheKey? other)
        {
            return other is not null
                && _contextType == other._contextType
                && ReferenceEquals(_databaseFieldProtector, other._databaseFieldProtector)
                && _designTime == other._designTime;
        }

        public override bool Equals(object? obj)
        {
            return obj is CottonModelCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            int protectorHash = _databaseFieldProtector is null
                ? 0
                : RuntimeHelpers.GetHashCode(_databaseFieldProtector);
            return HashCode.Combine(_contextType, protectorHash, _designTime);
        }
    }
}
