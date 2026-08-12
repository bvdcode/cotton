// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    internal class LayoutMutationGateScope
    {
        private int _depth;
        private IAsyncDisposable? _innerLease;

        public bool IsHeld { get; private set; }

        public void MarkHeld(IAsyncDisposable innerLease)
        {
            _innerLease = innerLease;
            IsHeld = true;
            _depth = 1;
        }

        public void Abandon()
        {
            _innerLease = null;
            IsHeld = false;
            _depth = 0;
        }

        public void Enter()
        {
            if (!IsHeld)
            {
                throw new InvalidOperationException("Layout mutation gate scope is not held.");
            }

            _depth++;
        }

        public bool Exit()
        {
            if (!IsHeld || _depth == 0)
            {
                throw new InvalidOperationException("Layout mutation gate scope is not held.");
            }

            _depth--;
            if (_depth > 0)
            {
                return true;
            }

            IsHeld = false;
            return false;
        }

        public ValueTask ReleaseAsync()
        {
            IAsyncDisposable innerLease = _innerLease
                ?? throw new InvalidOperationException("Layout mutation gate scope has no inner lease.");
            _innerLease = null;
            return innerLease.DisposeAsync();
        }
    }
}
