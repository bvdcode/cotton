// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Models.Results
{
    public class SharedFileHeadResult(
        string contentType,
        long contentLength,
        EntityTagHeaderValue entityTag,
        string fileName,
        bool inline) : ActionResult
    {
        public override Task ExecuteResultAsync(ActionContext context)
        {
            HttpResponse response = context.HttpContext.Response;
            FileResponseSecurity.ApplyFileResponseHeaders(response, contentType, inline);
            response.Headers.ContentEncoding = "identity";
            response.Headers.CacheControl = "private, no-store, no-transform";
            response.ContentType = FileResponseSecurity.ResolveContentTypeForResponse(contentType, inline);
            response.ContentLength = contentLength;
            response.Headers.ETag = entityTag.ToString();
            ContentDispositionHeaderValue disposition = new(
                FileResponseSecurity.ResolveContentDispositionType(contentType, inline))
            {
                FileNameStar = fileName,
                FileName = fileName,
            };
            response.Headers.ContentDisposition = disposition.ToString();
            return Task.CompletedTask;
        }
    }
}
