// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Continuous;
using Cotton.Sync.App.LocalChanges;
using Cotton.Sync.App.Supervision;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.Auth;

/// <summary>
/// Coordinates shutdown and local session cleanup when the server revokes the active session.
/// </summary>
public sealed class SessionRevocationHandler : ISessionRevocationHandler
{
    private readonly IAuthFlow _authFlow;
    private readonly ILocalChangeSyncCoordinator _localChanges;
    private readonly ILogger<SessionRevocationHandler> _logger;
    private readonly IPeriodicSyncCoordinator _periodicSync;
    private readonly ISyncSupervisor _supervisor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionRevocationHandler" /> class.
    /// </summary>
    public SessionRevocationHandler(
        IAuthFlow authFlow,
        ILocalChangeSyncCoordinator localChanges,
        IPeriodicSyncCoordinator periodicSync,
        ISyncSupervisor supervisor,
        ILogger<SessionRevocationHandler>? logger = null)
    {
        _authFlow = authFlow ?? throw new ArgumentNullException(nameof(authFlow));
        _localChanges = localChanges ?? throw new ArgumentNullException(nameof(localChanges));
        _periodicSync = periodicSync ?? throw new ArgumentNullException(nameof(periodicSync));
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _logger = logger ?? NullLogger<SessionRevocationHandler>.Instance;
    }

    /// <inheritdoc />
    public async Task HandleSessionRevokedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Authentication session was revoked by the server.");
        await ExecuteStepAsync(
            "periodic sync coordinator",
            token => _periodicSync.StopAsync(token),
            cancellationToken).ConfigureAwait(false);
        await ExecuteStepAsync(
            "local change coordinator",
            token => _localChanges.StopAsync(token),
            cancellationToken).ConfigureAwait(false);
        await ExecuteStepAsync(
            "authentication flow",
            token => _authFlow.SignOutAsync(token),
            cancellationToken).ConfigureAwait(false);
        await ExecuteStepAsync(
            "sync supervisor",
            token => _supervisor.StopAsync(token),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteStepAsync(
        string stepName,
        Func<CancellationToken, Task> step,
        CancellationToken cancellationToken)
    {
        try
        {
            await step(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to complete session revocation step {StepName}.",
                stepName);
        }
    }
}
