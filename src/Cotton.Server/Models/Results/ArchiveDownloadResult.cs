// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Models.Results
{
    public class ArchiveDownloadResult(ArchiveDownloadTicket ticket) : FileResult("application/zip")
    {
        public ArchiveDownloadTicket Ticket { get; } = ticket;

        public override Task ExecuteResultAsync(ActionContext context)
        {
            return context.HttpContext.RequestServices.GetRequiredService<ArchiveDownloadResultExecutor>()
                .ExecuteAsync(context, this);
        }
    }
}
