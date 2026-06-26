// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    /// <summary>
    /// Tracks a layout mutation semaphore held by the current async control flow.
    /// </summary>
    internal class LayoutMutationGateScope(SemaphoreSlim gate)
    {
        private int _depth;

        public bool IsHeld { get; private set; }

        public void MarkHeld()
        {
            IsHeld = true;
            _depth = 1;
        }

        public void Abandon()
        {
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

        public void Release()
        {
            gate.Release();
        }
    }
}
