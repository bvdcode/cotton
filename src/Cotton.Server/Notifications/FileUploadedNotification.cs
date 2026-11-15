// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Notifications
{
    public record FileUploadedNotification(Guid OwnerId, Guid NodeId, Guid NodeFileId, Guid? ShareId = null) : INotification;
}
