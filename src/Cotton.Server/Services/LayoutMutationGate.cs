// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using System.Collections.Concurrent;

namespace Cotton.Server.Services
{
    /// <summary>
    /// In-process semaphore gate for layout namespace mutations.
    /// </summary>
    public class LayoutMutationGate : ILayoutMutationGate
    {
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();
        private readonly AsyncLocal<Dictionary<Guid, LayoutMutationGateScope>?> _activeScopes = new();

        /// <inheritdoc />
        public Task<IAsyncDisposable> EnterAsync(Guid layoutId, CancellationToken cancellationToken)
        {
            Dictionary<Guid, LayoutMutationGateScope>? activeScopes = _activeScopes.Value;
            if (activeScopes is not null
                && activeScopes.TryGetValue(layoutId, out LayoutMutationGateScope? activeScope)
                && activeScope is not null)
            {
                if (activeScope.IsHeld)
                {
                    activeScope.Enter();
                    return Task.FromResult<IAsyncDisposable>(new LayoutMutationGateLease(this, layoutId, activeScope));
                }

                RemoveScope(layoutId, activeScopes, activeScope);
                activeScopes = _activeScopes.Value;
            }

            SemaphoreSlim gate = _gates.GetOrAdd(layoutId, _ => new SemaphoreSlim(1, 1));
            activeScopes ??= [];
            _activeScopes.Value = activeScopes;

            var scope = new LayoutMutationGateScope(gate);
            activeScopes.Add(layoutId, scope);

            Task waitTask = gate.WaitAsync(cancellationToken);
            if (waitTask.IsCompletedSuccessfully)
            {
                scope.MarkHeld();
                return Task.FromResult<IAsyncDisposable>(new LayoutMutationGateLease(this, layoutId, scope));
            }

            return EnterAfterWaitAsync(layoutId, scope, waitTask);
        }

        internal void Exit(Guid layoutId, LayoutMutationGateScope scope)
        {
            Dictionary<Guid, LayoutMutationGateScope>? activeScopes = _activeScopes.Value;
            if (activeScopes is null
                || !activeScopes.TryGetValue(layoutId, out LayoutMutationGateScope? activeScope)
                || !ReferenceEquals(activeScope, scope))
            {
                throw new InvalidOperationException("Layout mutation gate lease is not active in the current async context.");
            }

            if (scope.Exit())
            {
                return;
            }

            RemoveScope(layoutId, activeScopes, scope);
            scope.Release();
        }

        private async Task<IAsyncDisposable> EnterAfterWaitAsync(
            Guid layoutId,
            LayoutMutationGateScope scope,
            Task waitTask)
        {
            try
            {
                await waitTask;
            }
            catch
            {
                scope.Abandon();
                RemoveScope(layoutId, _activeScopes.Value, scope);
                throw;
            }

            scope.MarkHeld();
            return new LayoutMutationGateLease(this, layoutId, scope);
        }

        private void RemoveScope(
            Guid layoutId,
            Dictionary<Guid, LayoutMutationGateScope>? activeScopes,
            LayoutMutationGateScope scope)
        {
            if (activeScopes is null
                || !activeScopes.TryGetValue(layoutId, out LayoutMutationGateScope? activeScope)
                || !ReferenceEquals(activeScope, scope))
            {
                return;
            }

            activeScopes.Remove(layoutId);
            if (activeScopes.Count == 0)
            {
                _activeScopes.Value = null;
            }
        }
    }
}
