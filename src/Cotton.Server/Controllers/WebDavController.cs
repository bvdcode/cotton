// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Handlers.WebDav;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route("api/v1/webdav")]
    [Route("api/v1/webdav/{**path}")]
    public class WebDavController(
        IMediator _mediator,
        ILogger<WebDavController> _logger,
        WebDavLockManager _locks) : ControllerBase
    {
        [HttpOptions]
        [AllowAnonymous]
        public IActionResult HandleOptions()
        {
            AddDavHeaders();

            _locks.CleanupExpiredLocks();
            Response.Headers["Public"] = "OPTIONS, PROPFIND, GET, HEAD, PUT, DELETE, MKCOL, MOVE, COPY, LOCK, UNLOCK";
            return Ok();
        }

        [AcceptVerbs("PROPFIND")]
        [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
        public async Task<IActionResult> HandlePropFindAsync(string? path)
        {
            Guid userId = User.GetUserId();
            int depth = WebDavRequestHeaders.GetDepth(Request.Headers);
            string hrefBase = Url.Content("~" + WebDavRequestHeaders.WebDavRoute)
                ?? WebDavRequestHeaders.WebDavRoute;

            _logger.LogDebug("WebDAV PROPFIND: {Path}, depth: {Depth}, user: {UserId}, ip: {Ip}",
                path ?? "/", depth, userId, Request.GetRemoteAddress());

            WebDavPropFindQuery query = new WebDavPropFindQuery(userId, path ?? string.Empty, hrefBase, depth);
            WebDavPropFindResult result = await _mediator.Send(query, HttpContext.RequestAborted);

            if (!result.Found)
            {
                return NotFound();
            }

            AddDavHeaders();
            return new ContentResult
            {
                StatusCode = StatusCodes.Status207MultiStatus,
                ContentType = "application/xml; charset=\"utf-8\"",
                Content = result.XmlResponse
            };
        }

        [HttpGet]
        [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
        public async Task<IActionResult> HandleGetAsync(string? path)
        {
            Guid userId = User.GetUserId();

            _logger.LogDebug("WebDAV GET: {Path}, user: {UserId}, ip: {Ip}",
                path ?? "/", userId, Request.GetRemoteAddress());

            WebDavGetFileQuery query = new WebDavGetFileQuery(userId, path ?? string.Empty);
            WebDavGetFileResult result = await _mediator.Send(query, HttpContext.RequestAborted);

            if (!result.Found)
            {
                return NotFound();
            }

            if (result.IsCollection)
            {
                AddDavHeaders(exclude: ["GET", "HEAD", "PUT"]);
                return StatusCode(StatusCodes.Status405MethodNotAllowed, "Cannot GET a collection");
            }

            AddDavHeaders();
            ApplyFileResponseSecurity(result.ContentType, result.FileName);
            Response.Headers.ContentEncoding = "identity";
            Response.Headers.CacheControl = "private, no-store, no-transform";

            EntityTagHeaderValue entityTag = result.ETag is not null
                ? EntityTagHeaderValue.Parse(result.ETag)
                : throw new InvalidOperationException("ETag is required for file response");

            return File(
                result.Content ?? Stream.Null,
                result.ContentType ?? "application/octet-stream",
                fileDownloadName: null,
                lastModified: result.LastModified,
                entityTag: entityTag,
                enableRangeProcessing: true);
        }

        [HttpHead]
        [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
        public async Task<IActionResult> HandleHeadAsync(string? path)
        {
            Guid userId = User.GetUserId();
            WebDavHeadQuery query = new WebDavHeadQuery(userId, path ?? string.Empty);
            WebDavHeadResult result = await _mediator.Send(query, HttpContext.RequestAborted);

            if (!result.Found)
            {
                return NotFound();
            }

            if (result.IsCollection)
            {
                AddDavHeaders(exclude: ["GET", "HEAD", "PUT"]);
                return StatusCode(StatusCodes.Status405MethodNotAllowed, "Cannot HEAD a collection");
            }

            AddDavHeaders();
            ApplyFileResponseSecurity(result.ContentType, result.FileName);
            Response.ContentType = result.ContentType ?? "application/octet-stream";
            Response.ContentLength = result.ContentLength;
            Response.Headers.AcceptRanges = "bytes";
            Response.Headers.ContentEncoding = "identity";
            Response.Headers.CacheControl = "private, no-store, no-transform";

            if (result.LastModified.HasValue)
            {
                Response.Headers.LastModified = result.LastModified.Value.ToString("R");
            }

            if (result.ETag is not null)
            {
                Response.Headers.ETag = result.ETag;
            }

            return Ok();
        }

        private void ApplyFileResponseSecurity(string? contentType, string? fileName)
        {
            FileResponseSecurity.ApplyFileResponseHeaders(Response, contentType, requestedInline: true);
            if (!FileResponseSecurity.IsDangerousInlineContentType(contentType))
            {
                return;
            }

            ContentDispositionHeaderValue contentDisposition = new("attachment")
            {
                FileNameStar = fileName,
            };
            Response.Headers.ContentDisposition = contentDisposition.ToString();
        }

        [HttpPut]
        [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> HandlePutAsync(string? path)
        {
            Guid userId = User.GetUserId();
            if (!IsLockSatisfied(userId, path ?? string.Empty))
            {
                AddDavHeaders();
                return StatusCode(StatusCodes.Status423Locked, "Resource is locked");
            }
            bool overwrite = WebDavRequestHeaders.GetOverwrite(Request.Headers);
            string? contentType = Request.ContentType;

            WebDavPutFileRequest command = new WebDavPutFileRequest(
                userId,
                path ?? string.Empty,
                Request.Body,
                contentType,
                overwrite,
                Request.ContentLength);

            WebDavPutFileResult result = await _mediator.Send(command, HttpContext.RequestAborted);
            if (!result.Success)
            {
                if (result.Error == WebDavPutFileError.IsCollection)
                {
                    AddDavHeaders(exclude: ["GET", "HEAD", "PUT"]);
                }
                return result.Error switch
                {
                    WebDavPutFileError.ParentNotFound => Conflict("Parent collection not found"),
                    WebDavPutFileError.IsCollection => Conflict("Cannot PUT to a collection"),
                    WebDavPutFileError.InvalidName => BadRequest("Invalid resource name"),
                    WebDavPutFileError.Conflict => Conflict("Conflict with existing resource"),
                    WebDavPutFileError.PreconditionFailed => StatusCode(StatusCodes.Status412PreconditionFailed, "Destination exists and Overwrite is false"),
                    WebDavPutFileError.UploadAborted => StatusCode(StatusCodes.Status408RequestTimeout, "Upload aborted"),
                    WebDavPutFileError.QuotaExceeded => StatusCode(507, "Storage quota exceeded"),
                    WebDavPutFileError.StoragePressure => StatusCode(507, "Storage is running out of free space"),
                    _ => StatusCode(StatusCodes.Status500InternalServerError)
                };
            }

            AddDavHeaders();
            return result.Created ? Created() : NoContent();
        }

        [AcceptVerbs("PROPPATCH")]
        [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
        public async Task<IActionResult> HandlePropPatchAsync(string? path)
        {
            Guid userId = User.GetUserId();
            path ??= string.Empty;

            WebDavHeadQuery query = new WebDavHeadQuery(userId, path);
            WebDavHeadResult result = await _mediator.Send(query, HttpContext.RequestAborted);
            if (!result.Found)
            {
                return NotFound();
            }

            AddDavHeaders();

            string hrefBase = Url.Content("~" + WebDavRequestHeaders.WebDavRoute)
                ?? WebDavRequestHeaders.WebDavRoute;
            string href = hrefBase.TrimEnd(WebDavPathResolver.PathSeparator)
                + WebDavPathResolver.PathSeparator
                + path.TrimStart(WebDavPathResolver.PathSeparator);
            string xml = WebDavXmlBuilder.BuildPropPatchOkResponse(href);

            return new ContentResult
            {
                StatusCode = StatusCodes.Status207MultiStatus,
                ContentType = "application/xml; charset=\"utf-8\"",
                Content = xml
            };
        }

        [AcceptVerbs("LOCK")]
        [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
        public async Task<IActionResult> HandleLockAsync(string? path)
        {
            Guid userId = User.GetUserId();
            path ??= string.Empty;

            // Allow lock-null resources (common behavior in Windows WebDAV)
            WebDavHeadQuery query = new WebDavHeadQuery(userId, path);
            WebDavHeadResult result = await _mediator.Send(query, HttpContext.RequestAborted);

            AddDavHeaders();

            TimeSpan timeout = WebDavRequestHeaders.GetLockTimeout(Request.Headers);
            WebDavLockInfo lockInfo = _locks.Create(userId, path, timeout);
            Response.Headers["Lock-Token"] = $"<{lockInfo.Token}>";
            Response.Headers["Timeout"] = $"Second-{(int)timeout.TotalSeconds}";

            string xml = WebDavXmlBuilder.BuildLockDiscoveryResponse(lockInfo.Token, timeout);
            if (!result.Found)
            {
                return new ContentResult
                {
                    StatusCode = StatusCodes.Status201Created,
                    ContentType = "application/xml; charset=\"utf-8\"",
                    Content = xml
                };
            }

            return Content(xml, "application/xml; charset=\"utf-8\"");
        }

        [AcceptVerbs("UNLOCK")]
        [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
        public IActionResult HandleUnlock(string? path)
        {
            Guid userId = User.GetUserId();
            path ??= string.Empty;

            AddDavHeaders();

            string? lockToken = WebDavRequestHeaders.GetLockToken(Request.Headers);
            if (lockToken is not null)
            {
                _locks.Unlock(userId, path, lockToken);
            }

            return NoContent();
        }

        [HttpDelete]
        [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
        public async Task<IActionResult> HandleDeleteAsync(string? path)
        {
            Guid userId = User.GetUserId();
            if (!IsLockSatisfied(userId, path ?? string.Empty))
            {
                AddDavHeaders();
                return StatusCode(StatusCodes.Status423Locked, "Resource is locked");
            }
            WebDavDeleteRequest command = new WebDavDeleteRequest(userId, path ?? string.Empty);
            WebDavDeleteResult result = await _mediator.Send(command, HttpContext.RequestAborted);

            AddDavHeaders();

            if (result.NotFound)
            {
                return NotFound();
            }

            if (!result.Success)
            {
                return Forbid();
            }

            return NoContent();
        }

        [AcceptVerbs("MKCOL")]
        [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
        public async Task<IActionResult> HandleMkColAsync(string? path)
        {
            Guid userId = User.GetUserId();
            if (!IsLockSatisfied(userId, path ?? string.Empty))
            {
                AddDavHeaders();
                return StatusCode(StatusCodes.Status423Locked, "Resource is locked");
            }
            WebDavMkColRequest command = new WebDavMkColRequest(userId, path ?? string.Empty);
            WebDavMkColResult result = await _mediator.Send(command, HttpContext.RequestAborted);
            AddDavHeaders();
            if (!result.Success)
            {
                return result.Error switch
                {
                    WebDavMkColError.ParentNotFound => Conflict("Parent collection not found"),
                    WebDavMkColError.AlreadyExists => StatusCode(StatusCodes.Status405MethodNotAllowed, "Collection already exists"),
                    WebDavMkColError.InvalidName => BadRequest("Invalid collection name"),
                    WebDavMkColError.Conflict => Conflict("Conflict with existing resource"),
                    _ => StatusCode(StatusCodes.Status500InternalServerError)
                };
            }
            return Created();
        }

        [AcceptVerbs("MOVE")]
        [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
        public async Task<IActionResult> HandleMoveAsync(string? path)
        {
            Guid userId = User.GetUserId();
            if (!IsLockSatisfied(userId, path ?? string.Empty))
            {
                AddDavHeaders();
                return StatusCode(StatusCodes.Status423Locked, "Resource is locked");
            }
            string? destination = WebDavRequestHeaders.GetDestinationPath(Request.Headers);
            bool overwrite = WebDavRequestHeaders.GetOverwrite(Request.Headers);

            if (string.IsNullOrEmpty(destination))
            {
                return BadRequest("Destination header is required");
            }

            WebDavMoveRequest command = new WebDavMoveRequest(userId, path ?? string.Empty, destination, overwrite);
            WebDavMoveResult result = await _mediator.Send(command, HttpContext.RequestAborted);
            AddDavHeaders();
            if (!result.Success)
            {
                return result.Error switch
                {
                    WebDavMoveError.SourceNotFound => NotFound(),
                    WebDavMoveError.DestinationParentNotFound => Conflict("Destination parent not found"),
                    WebDavMoveError.DestinationExists => StatusCode(412, "Destination exists and Overwrite is false"),
                    WebDavMoveError.InvalidName => BadRequest("Invalid resource name"),
                    WebDavMoveError.CannotMoveRoot => Forbid(),
                    WebDavMoveError.CannotMoveIntoDescendant => Conflict("Cannot move a collection into its descendant"),
                    _ => StatusCode(StatusCodes.Status500InternalServerError)
                };
            }
            return result.Created ? Created() : NoContent();
        }

        [AcceptVerbs("COPY")]
        [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
        public async Task<IActionResult> HandleCopyAsync(string? path)
        {
            Guid userId = User.GetUserId();
            if (!IsLockSatisfied(userId, path ?? string.Empty))
            {
                AddDavHeaders();
                return StatusCode(StatusCodes.Status423Locked, "Resource is locked");
            }
            string? destination = WebDavRequestHeaders.GetDestinationPath(Request.Headers);
            bool overwrite = WebDavRequestHeaders.GetOverwrite(Request.Headers);

            if (string.IsNullOrEmpty(destination))
            {
                return BadRequest("Destination header is required");
            }
            WebDavCopyRequest command = new WebDavCopyRequest(userId, path ?? string.Empty, destination, overwrite);
            WebDavCopyResult result = await _mediator.Send(command, HttpContext.RequestAborted);
            AddDavHeaders();
            if (!result.Success)
            {
                return result.Error switch
                {
                    WebDavCopyError.SourceNotFound => NotFound(),
                    WebDavCopyError.DestinationParentNotFound => Conflict("Destination parent not found"),
                    WebDavCopyError.DestinationExists => StatusCode(StatusCodes.Status412PreconditionFailed, "Destination exists and Overwrite is false"),
                    WebDavCopyError.InvalidName => BadRequest("Invalid resource name"),
                    WebDavCopyError.CannotCopyRoot => Forbid(),
                    WebDavCopyError.QuotaExceeded => StatusCode(507, "Storage quota exceeded"),
                    _ => StatusCode(StatusCodes.Status500InternalServerError)
                };
            }

            return result.Created ? Created() : NoContent();
        }

        private void AddDavHeaders(params string[] exclude)
        {
            string[] methods =
            [
                "OPTIONS", "PROPFIND", "PROPPATCH", "GET", "HEAD", "PUT", "DELETE", "MKCOL", "MOVE", "COPY", "LOCK", "UNLOCK"
            ];

            HashSet<string> excludeSet = new HashSet<string>(exclude, StringComparer.OrdinalIgnoreCase);
            Response.Headers["DAV"] = "1, 2";
            Response.Headers["MS-Author-Via"] = "DAV";
            Response.Headers.Allow = string.Join(", ",
                methods.Where(m => !excludeSet.Contains(m)));
        }

        private bool IsLockSatisfied(Guid userId, string path)
        {
            return _locks.IsSatisfied(
                userId,
                path,
                WebDavRequestHeaders.GetLockToken(Request.Headers));
        }
    }
}
