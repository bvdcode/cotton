// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Handlers.WebDav;
using Cotton.Server.Services.WebDav;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Controllers;

/// <summary>
/// WebDAV controller for file management via WebDAV protocol.
/// Supports: OPTIONS, PROPFIND, GET, HEAD, PUT, DELETE, MKCOL, MOVE, COPY
/// </summary>
[ApiController]
[Route("api/v1/webdav")]
[Route("api/v1/webdav/{**path}")]
public class WebDavController(
    IMediator _mediator,
    ILogger<WebDavController> _logger) : ControllerBase
{
    private const string WebDavRoute = "/api/v1/webdav/";
    private static readonly string WebDavPrefix = WebDavRoute.TrimEnd('/');

    private sealed record WebDavLock(
        Guid UserId,
        string Path,
        string Token,
        DateTimeOffset ExpiresAt);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, WebDavLock> _locks = new();

    private static long _lastLocksCleanupTicks;
    private static readonly long LocksCleanupIntervalTicks = TimeSpan.FromSeconds(30).Ticks;
    private static string GetLockKey(Guid userId, string path) => $"{userId:N}:{path}";

    [HttpOptions]
    [AllowAnonymous]
    public IActionResult HandleOptions()
    {
        AddDavHeaders();

        CleanupExpiredLocksIfNeeded(force: true);
        Response.Headers["Public"] = "OPTIONS, PROPFIND, GET, HEAD, PUT, DELETE, MKCOL, MOVE, COPY, LOCK, UNLOCK";
        return Ok();
    }

    [AcceptVerbs("PROPFIND")]
    [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
    public async Task<IActionResult> HandlePropFindAsync(string? path)
    {
        var userId = User.GetUserId();
        var depth = GetDepthHeader();
        var hrefBase = Url.Content("~" + WebDavRoute) ?? WebDavRoute;

        _logger.LogDebug("WebDAV PROPFIND: {Path}, depth: {Depth}, user: {UserId}, ip: {Ip}",
            path ?? "/", depth, userId, Request.GetRemoteAddress());

        var query = new WebDavPropFindQuery(userId, path ?? string.Empty, hrefBase, depth);
        var result = await _mediator.Send(query, HttpContext.RequestAborted);

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
        var userId = User.GetUserId();

        _logger.LogDebug("WebDAV GET: {Path}, user: {UserId}, ip: {Ip}",
            path ?? "/", userId, Request.GetRemoteAddress());

        var query = new WebDavGetFileQuery(userId, path ?? string.Empty);
        var result = await _mediator.Send(query, HttpContext.RequestAborted);

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
        Response.Headers.ContentEncoding = "identity";
        Response.Headers.CacheControl = "private, no-store, no-transform";

        var entityTag = result.ETag is not null
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
        var userId = User.GetUserId();
        var query = new WebDavHeadQuery(userId, path ?? string.Empty);
        var result = await _mediator.Send(query, HttpContext.RequestAborted);

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

    [HttpPut]
    [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> HandlePutAsync(string? path)
    {
        var userId = User.GetUserId();
        if (!IsLockSatisfied(userId, path ?? string.Empty))
        {
            AddDavHeaders();
            return StatusCode(StatusCodes.Status423Locked, "Resource is locked");
        }
        var overwrite = GetOverwriteHeader();
        var contentType = Request.ContentType;

        var command = new WebDavPutFileCommand(
            userId,
            path ?? string.Empty,
            Request.Body,
            contentType,
            overwrite,
            Request.ContentLength);

        var result = await _mediator.Send(command, HttpContext.RequestAborted);
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
        var userId = User.GetUserId();
        path ??= string.Empty;

        var query = new WebDavHeadQuery(userId, path);
        var result = await _mediator.Send(query, HttpContext.RequestAborted);
        if (!result.Found)
        {
            return NotFound();
        }

        AddDavHeaders();

        var hrefBase = Url.Content("~" + WebDavRoute) ?? WebDavRoute;
        var href = hrefBase.TrimEnd('/') + "/" + path.TrimStart('/');
        var xml = WebDavXmlBuilder.BuildPropPatchOkResponse(href);

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
        var userId = User.GetUserId();
        path ??= string.Empty;

        // Allow lock-null resources (common behavior in Windows WebDAV)
        var query = new WebDavHeadQuery(userId, path);
        var result = await _mediator.Send(query, HttpContext.RequestAborted);

        AddDavHeaders();

        var timeoutHeader = Request.Headers["Timeout"].ToString();
        TimeSpan timeout = TimeSpan.FromHours(1);
        if (!string.IsNullOrWhiteSpace(timeoutHeader)
            && timeoutHeader.StartsWith("Second-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(timeoutHeader["Second-".Length..], out var seconds)
            && seconds > 0)
        {
            timeout = TimeSpan.FromSeconds(seconds);
        }

        var token = $"opaquelocktoken:{Guid.NewGuid():D}";
        var lockInfo = new WebDavLock(
            userId,
            path.Trim('/'),
            token,
            DateTimeOffset.UtcNow.Add(timeout));

        _locks[GetLockKey(userId, lockInfo.Path)] = lockInfo;
        Response.Headers["Lock-Token"] = $"<{token}>";
        Response.Headers["Timeout"] = $"Second-{(int)timeout.TotalSeconds}";

        var xml = WebDavXmlBuilder.BuildLockDiscoveryResponse(token, timeout);
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
    public IActionResult HandleUnlockAsync(string? path)
    {
        var userId = User.GetUserId();
        path ??= string.Empty;

        AddDavHeaders();

        var tokenHeader = Request.Headers["Lock-Token"].ToString();
        if (!string.IsNullOrWhiteSpace(tokenHeader))
        {
            var token = tokenHeader.Trim().Trim('<', '>');
            var key = GetLockKey(userId, path.Trim('/'));

            if (_locks.TryGetValue(key, out var info)
                && string.Equals(info.Token, token, StringComparison.Ordinal))
            {
                _locks.TryRemove(key, out _);
            }
        }

        return NoContent();
    }

    [HttpDelete]
    [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
    public async Task<IActionResult> HandleDeleteAsync(string? path)
    {
        var userId = User.GetUserId();
        if (!IsLockSatisfied(userId, path ?? string.Empty))
        {
            AddDavHeaders();
            return StatusCode(StatusCodes.Status423Locked, "Resource is locked");
        }
        var command = new WebDavDeleteCommand(userId, path ?? string.Empty);
        var result = await _mediator.Send(command, HttpContext.RequestAborted);

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
        var userId = User.GetUserId();
        if (!IsLockSatisfied(userId, path ?? string.Empty))
        {
            AddDavHeaders();
            return StatusCode(StatusCodes.Status423Locked, "Resource is locked");
        }
        var command = new WebDavMkColCommand(userId, path ?? string.Empty);
        var result = await _mediator.Send(command, HttpContext.RequestAborted);
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
        var userId = User.GetUserId();
        if (!IsLockSatisfied(userId, path ?? string.Empty))
        {
            AddDavHeaders();
            return StatusCode(StatusCodes.Status423Locked, "Resource is locked");
        }
        var destination = GetDestinationPath();
        var overwrite = GetOverwriteHeader();

        if (string.IsNullOrEmpty(destination))
        {
            return BadRequest("Destination header is required");
        }

        var command = new WebDavMoveCommand(userId, path ?? string.Empty, destination, overwrite);
        var result = await _mediator.Send(command, HttpContext.RequestAborted);
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
        var userId = User.GetUserId();
        if (!IsLockSatisfied(userId, path ?? string.Empty))
        {
            AddDavHeaders();
            return StatusCode(StatusCodes.Status423Locked, "Resource is locked");
        }
        var destination = GetDestinationPath();
        var overwrite = GetOverwriteHeader();

        if (string.IsNullOrEmpty(destination))
        {
            return BadRequest("Destination header is required");
        }
        var command = new WebDavCopyCommand(userId, path ?? string.Empty, destination, overwrite);
        var result = await _mediator.Send(command, HttpContext.RequestAborted);
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

        var excludeSet = new HashSet<string>(exclude, StringComparer.OrdinalIgnoreCase);
        Response.Headers["DAV"] = "1, 2";
        Response.Headers["MS-Author-Via"] = "DAV";
        Response.Headers.Allow = string.Join(", ",
            methods.Where(m => !excludeSet.Contains(m)));
    }

    private bool IsLockSatisfied(Guid userId, string path)
    {
        path = (path ?? string.Empty).Trim('/');

        CleanupExpiredLocksIfNeeded(force: false);

        // Check exact and all parents: "a/b/c" -> "a/b/c", "a/b", "a", ""
        for (var p = path; ; p = ParentPath(p))
        {
            var key = GetLockKey(userId, p);
            if (_locks.TryGetValue(key, out var lockInfo))
            {
                var lockToken = ExtractLockToken();
                return lockToken is not null
                       && string.Equals(lockToken, lockInfo.Token, StringComparison.Ordinal);
            }

            if (string.IsNullOrEmpty(p))
            {
                break;
            }
        }

        return true;
    }

    private static string ParentPath(string path)
    {
        var i = path.LastIndexOf('/');
        return i < 0 ? string.Empty : path[..i];
    }

    private static void CleanupExpiredLocksIfNeeded(bool force)
    {
        var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
        var last = System.Threading.Interlocked.Read(ref _lastLocksCleanupTicks);
        if (!force && nowTicks - last < LocksCleanupIntervalTicks)
        {
            return;
        }

        if (System.Threading.Interlocked.Exchange(ref _lastLocksCleanupTicks, nowTicks) == last || force)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var (key, value) in _locks)
            {
                if (value.ExpiresAt <= now)
                {
                    _locks.TryRemove(key, out _);
                }
            }
        }
    }

    private string? ExtractLockToken()
    {
        var lockTokenHeader = Request.Headers["Lock-Token"].ToString();
        if (!string.IsNullOrWhiteSpace(lockTokenHeader))
        {
            return lockTokenHeader.Trim().Trim('<', '>');
        }

        var ifHeader = Request.Headers["If"].ToString();
        if (string.IsNullOrWhiteSpace(ifHeader))
        {
            return null;
        }

        // Very small parser: just find first <opaquelocktoken:...>
        var start = ifHeader.IndexOf("<opaquelocktoken:", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        var end = ifHeader.IndexOf('>', start);
        if (end < 0)
        {
            return null;
        }

        return ifHeader[(start + 1)..end];
    }

    private int GetDepthHeader()
    {
        var depthHeader = Request.Headers["Depth"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(depthHeader))
        {
            return 1;
        }

        depthHeader = depthHeader.Split(',')[0].Trim();
        if (depthHeader == "0")
        {
            return 0;
        }

        if (depthHeader == "1")
        {
            return 1;
        }

        if (string.Equals(depthHeader, "infinity", StringComparison.OrdinalIgnoreCase))
        {
            return 25;
        }

        return 1;
    }

    private string? GetDestinationPath()
    {
        var destination = Request.Headers["Destination"].FirstOrDefault();
        if (string.IsNullOrEmpty(destination))
        {
            return null;
        }

        // Parse the destination URL and extract the path
        if (Uri.TryCreate(destination, UriKind.RelativeOrAbsolute, out var uri))
        {
            destination = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;
        }

        destination = Uri.UnescapeDataString(destination);

        // Remove the WebDAV route prefix (with or without trailing slash)
        var idx = destination.IndexOf(WebDavPrefix, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            destination = destination[(idx + WebDavPrefix.Length)..];
            destination = destination.TrimStart('/');
        }

        return destination.Trim('/');
    }

    private bool GetOverwriteHeader()
    {
        var overwrite = Request.Headers["Overwrite"].FirstOrDefault();
        return !string.Equals(overwrite, "F", StringComparison.OrdinalIgnoreCase);
    }
}
