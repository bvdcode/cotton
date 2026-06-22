// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Database;
using Cotton.Server.Hubs;
using Cotton.Server.Models.Dto;
using Mapster;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Cotton.Database.Models;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Defines the event notification service contract used by the server runtime.
    /// </summary>
    public interface IEventNotificationService
    {
        /// <summary>
        /// Notifies connected clients that file created occurred.
        /// </summary>
        Task NotifyFileCreatedAsync(Guid nodeFileId, CancellationToken ct = default);

        /// <summary>
        /// Notifies connected clients that file updated occurred.
        /// </summary>
        Task NotifyFileUpdatedAsync(Guid nodeFileId, CancellationToken ct = default);

        /// <summary>
        /// Notifies connected clients that file deleted occurred.
        /// </summary>
        Task NotifyFileDeletedAsync(Guid userId, Guid nodeFileId, Guid? parentNodeId, CancellationToken ct = default);

        /// <summary>
        /// Notifies connected clients that file moved occurred.
        /// </summary>
        Task NotifyFileMovedAsync(Guid nodeFileId, Guid oldParentId, CancellationToken ct = default);

        /// <summary>
        /// Notifies connected clients that file renamed occurred.
        /// </summary>
        Task NotifyFileRenamedAsync(Guid nodeFileId, CancellationToken ct = default);

        /// <summary>
        /// Notifies connected clients that node created occurred.
        /// </summary>
        Task NotifyNodeCreatedAsync(Guid nodeId, CancellationToken ct = default);

        /// <summary>
        /// Notifies connected clients that node deleted occurred.
        /// </summary>
        Task NotifyNodeDeletedAsync(Guid userId, Guid nodeId, Guid? parentNodeId, CancellationToken ct = default);

        /// <summary>
        /// Notifies connected clients that node moved occurred.
        /// </summary>
        Task NotifyNodeMovedAsync(Guid nodeId, Guid oldParentId, CancellationToken ct = default);

        /// <summary>
        /// Notifies connected clients that node renamed occurred.
        /// </summary>
        Task NotifyNodeRenamedAsync(Guid nodeId, CancellationToken ct = default);
    }
}
