// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Channels;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Sends administrator notifications when Cotton rejects a protected database row.
/// </summary>
/// <remarks>
/// Reporting must never become a second failure mode for authentication or token reads, so failures are pushed through a
/// bounded in-memory queue and deduplicated. If the queue is full, the protected operation still fails and the drop is
/// logged loudly for operators.
/// </remarks>
public sealed class DatabaseIntegrityFailureReporter(
    IServiceScopeFactory _scopeFactory,
    ILogger<DatabaseIntegrityFailureReporter> _logger) : BackgroundService, IDatabaseIntegrityFailureReporter
{
    private const int QueueCapacity = 256;
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromHours(6);
    private readonly Channel<DatabaseIntegrityFailure> _queue = Channel.CreateBounded<DatabaseIntegrityFailure>(
        new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly ConcurrentDictionary<string, DateTime> _recentFailures = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Report(DatabaseIntegrityFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        string key = CreateDedupeKey(failure);
        DateTime now = DateTime.UtcNow;
        if (_recentFailures.TryGetValue(key, out DateTime lastSeen)
            && now - lastSeen < DedupeWindow)
        {
            return;
        }

        _recentFailures[key] = now;
        PruneDedupeCache(now);

        if (!_queue.Writer.TryWrite(failure))
        {
            _logger.LogError(
                "Database integrity failure notification queue is full. Dropped {EntityName} {EntityKey} at {Boundary}.",
                failure.EntityName,
                failure.EntityKey,
                failure.Boundary);
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (DatabaseIntegrityFailure failure in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await NotifyAdminsAsync(failure, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to notify admins about database integrity failure for {EntityName} {EntityKey}.",
                    failure.EntityName,
                    failure.EntityKey);
            }
        }
    }

    private async Task NotifyAdminsAsync(
        DatabaseIntegrityFailure failure,
        CancellationToken cancellationToken)
    {
        // Notifications are rendered from template metadata on the client when translations are available. The plain
        // strings remain as a safe display value for clients that do not yet know the template.
        using IServiceScope scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationsProvider>();

        List<Guid> adminIds = await dbContext.Users
            .Where(x => x.Role == UserRole.Admin)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (adminIds.Count == 0)
        {
            _logger.LogWarning(
                "Database integrity failure detected for {EntityName} {EntityKey}, but no admins exist to notify.",
                failure.EntityName,
                failure.EntityKey);
            return;
        }

        var metadata = NotificationTemplateMetadata.Create(
            NotificationTemplateKeys.DatabaseIntegrityFailureTitle,
            NotificationTemplateKeys.DatabaseIntegrityFailureContent,
            new Dictionary<string, string>
            {
                ["entityName"] = failure.EntityName,
                ["entityKey"] = failure.EntityKey,
                ["boundary"] = failure.Boundary,
                ["detectedAtUtc"] = failure.DetectedAtUtc.ToString("O"),
                ["detectedAtUtcDisplay"] = failure.DetectedAtUtc.ToString(
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture)
            });
        string content = NotificationTemplates.DatabaseIntegrityFailureContent(
            failure.EntityName,
            failure.EntityKey,
            failure.Boundary,
            failure.DetectedAtUtc);

        foreach (Guid adminId in adminIds)
        {
            await notifications.SendNotificationAsync(
                adminId,
                NotificationTemplates.DatabaseIntegrityFailureTitle,
                content,
                NotificationPriority.High,
                metadata);
        }
    }

    private static string CreateDedupeKey(DatabaseIntegrityFailure failure)
    {
        return string.Join(
            "|",
            failure.EntityName,
            failure.EntityKey,
            failure.Boundary);
    }

    private void PruneDedupeCache(DateTime now)
    {
        foreach ((string key, DateTime lastSeen) in _recentFailures)
        {
            if (now - lastSeen >= DedupeWindow)
            {
                _recentFailures.TryRemove(key, out _);
            }
        }
    }
}
