// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;

namespace Cotton.Server.Services
{
    public class LayoutMutationGate : ILayoutMutationGate
    {
        private readonly KeyedAsyncGate<Guid> _gates = new();
        private readonly AsyncLocal<Dictionary<Guid, LayoutMutationGateScope>?> _activeScopes = new();

        internal int Count => _gates.Count;

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

            activeScopes ??= [];
            _activeScopes.Value = activeScopes;

            LayoutMutationGateScope scope = new();
            activeScopes.Add(layoutId, scope);

            ValueTask<IAsyncDisposable> enterTask = _gates.EnterAsync(layoutId, cancellationToken);
            if (enterTask.IsCompletedSuccessfully)
            {
                scope.MarkHeld(enterTask.Result);
                return Task.FromResult<IAsyncDisposable>(new LayoutMutationGateLease(this, layoutId, scope));
            }

            return EnterAfterWaitAsync(layoutId, scope, enterTask);
        }

        internal ValueTask ExitAsync(Guid layoutId, LayoutMutationGateScope scope)
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
                return ValueTask.CompletedTask;
            }

            RemoveScope(layoutId, activeScopes, scope);
            return scope.ReleaseAsync();
        }

        private async Task<IAsyncDisposable> EnterAfterWaitAsync(
            Guid layoutId,
            LayoutMutationGateScope scope,
            ValueTask<IAsyncDisposable> enterTask)
        {
            IAsyncDisposable innerLease;
            try
            {
                innerLease = await enterTask;
            }
            catch
            {
                scope.Abandon();
                RemoveScope(layoutId, _activeScopes.Value, scope);
                throw;
            }

            scope.MarkHeld(innerLease);
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
