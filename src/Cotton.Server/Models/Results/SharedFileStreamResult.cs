// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Extensions;
using Cotton.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Models.Results
{
    public class SharedFileStreamResult : FileStreamResult
    {
        private readonly bool _inline;
        private readonly string _originalContentType;
        private readonly Guid? _deleteTokenId;

        public SharedFileStreamResult(
            Stream stream,
            string contentType,
            string fileName,
            bool inline,
            DateTimeOffset lastModified,
            EntityTagHeaderValue entityTag,
            Guid? deleteTokenId = null)
            : base(stream, FileResponseSecurity.ResolveContentTypeForResponse(contentType, inline))
        {
            _inline = inline;
            _originalContentType = contentType;
            _deleteTokenId = deleteTokenId;
            FileDownloadName = FileResponseSecurity.ResolveFileDownloadName(fileName, inline, contentType);
            LastModified = lastModified;
            EntityTag = entityTag;
            EnableRangeProcessing = true;
        }

        public override Task ExecuteResultAsync(ActionContext context)
        {
            HttpResponse response = context.HttpContext.Response;
            FileResponseSecurity.ApplyFileResponseHeaders(response, _originalContentType, _inline);
            response.Headers.ContentEncoding = "identity";
            response.Headers.CacheControl = "private, no-store, no-transform";
            if (_deleteTokenId is Guid tokenId)
            {
                CottonDbContext dbContext = context.HttpContext.RequestServices.GetRequiredService<CottonDbContext>();
                response.RegisterDeleteAfterUse(dbContext, tokenId);
            }

            return base.ExecuteResultAsync(context);
        }
    }
}
