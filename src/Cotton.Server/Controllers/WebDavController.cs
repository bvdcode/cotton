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
[Route("api/v1/webdav/{**path}")]
public class WebDavController(
    IMediator _mediator,
    IWebDavLockGuard _lockGuard) : ControllerBase
{
    private const string WebDavRoute = "/api/v1/webdav/";

    [HttpOptions]
    [AllowAnonymous]
    public IActionResult HandleOptions()
    {
        AddDavHeaders();
        Response.Headers["Public"] = "OPTIONS, PROPFIND, GET, HEAD, PUT, DELETE, MKCOL, MOVE, COPY, LOCK, UNLOCK";
        return Ok();
    }

    [AcceptVerbs("LOCK")]
    [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
    public async Task<IActionResult> HandleLockAsync(string? path)
    {
        var userId = User.GetUserId();
        var ifHeader = Request.Headers["If"].FirstOrDefault();
        var timeout = GetTimeoutHeader();

        var command = new WebDavLockCommand(userId, path ?? string.Empty, ifHeader, timeout);
        var result = await _mediator.Send(command);

        AddDavHeaders();

        if (!result.Success)
        {
            return result.Error switch
            {
                WebDavLockError.NotFound => NotFound(),
                WebDavLockError.Locked => StatusCode(StatusCodes.Status423Locked),
                WebDavLockError.PreconditionFailed => StatusCode(StatusCodes.Status412PreconditionFailed),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }

        if (result.Lock is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        Response.Headers["Lock-Token"] = $"<{result.Lock.Token}>";
        if (result.Lock.ExpiresAt.HasValue)
        {
            var seconds = Math.Max(1, (long)(result.Lock.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalSeconds);
            Response.Headers["Timeout"] = $"Second-{seconds}";
        }

        return new ContentResult
        {
            StatusCode = result.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK,
            ContentType = "application/xml; charset=\"utf-8\"",
            Content = WebDavLockXmlBuilder.BuildPropResponse(result.Lock)
        };
    }

    [AcceptVerbs("UNLOCK")]
    [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
    public async Task<IActionResult> HandleUnlockAsync(string? path)
    {
        var userId = User.GetUserId();
        var lockToken = Request.Headers["Lock-Token"].FirstOrDefault();
        var command = new WebDavUnlockCommand(userId, path ?? string.Empty, lockToken);
        var result = await _mediator.Send(command);

        AddDavHeaders();

        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        return NoContent();
    }

    [AcceptVerbs("PROPFIND")]
    [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
    public async Task<IActionResult> HandlePropFindAsync(string? path)
    {
        var userId = User.GetUserId();
        var depth = GetDepthHeader();
        var hrefBase = Url.Content("~" + WebDavRoute) ?? WebDavRoute;
        var query = new WebDavPropFindQuery(userId, path ?? string.Empty, hrefBase, depth);
        var result = await _mediator.Send(query);

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
        var query = new WebDavGetFileQuery(userId, path ?? string.Empty);
        var result = await _mediator.Send(query);

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
            : EntityTagHeaderValue.Any;

        return File(
            result.Content ?? Stream.Null,
            result.ContentType ?? "application/octet-stream",
            result.FileName,
            result.LastModified,
            entityTag,
            enableRangeProcessing: true);
    }

    [HttpHead]
    [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
    public async Task<IActionResult> HandleHeadAsync(string? path)
    {
        var userId = User.GetUserId();
        var query = new WebDavHeadQuery(userId, path ?? string.Empty);
        var result = await _mediator.Send(query);

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
        if (!_lockGuard.TryAuthorizeWrite(path ?? string.Empty, userId, Request.Headers["If"].FirstOrDefault(), out _))
        {
            AddDavHeaders();
            return StatusCode(StatusCodes.Status423Locked);
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

        var result = await _mediator.Send(command);
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
                WebDavPutFileError.UploadAborted => StatusCode(StatusCodes.Status500InternalServerError, "Upload aborted"),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }

        AddDavHeaders();
        return result.Created ? Created() : NoContent();
    }

    [HttpDelete]
    [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
    public async Task<IActionResult> HandleDeleteAsync(string? path)
    {
        var userId = User.GetUserId();
        if (!_lockGuard.TryAuthorizeWrite(path ?? string.Empty, userId, Request.Headers["If"].FirstOrDefault(), out _))
        {
            AddDavHeaders();
            return StatusCode(StatusCodes.Status423Locked);
        }

        var command = new WebDavDeleteCommand(userId, path ?? string.Empty);
        var result = await _mediator.Send(command);

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
        if (!_lockGuard.TryAuthorizeWrite(path ?? string.Empty, userId, Request.Headers["If"].FirstOrDefault(), out _))
        {
            AddDavHeaders();
            return StatusCode(StatusCodes.Status423Locked);
        }

        var command = new WebDavMkColCommand(userId, path ?? string.Empty);
        var result = await _mediator.Send(command);
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
        var destination = GetDestinationPath();
        var overwrite = GetOverwriteHeader();

        if (string.IsNullOrEmpty(destination))
        {
            return BadRequest("Destination header is required");
        }

        var ifHeader = Request.Headers["If"].FirstOrDefault();
        if (!_lockGuard.TryAuthorizeWrite(path ?? string.Empty, userId, ifHeader, out _)
            || !_lockGuard.TryAuthorizeWrite(destination, userId, ifHeader, out _))
        {
            AddDavHeaders();
            return StatusCode(StatusCodes.Status423Locked);
        }

        var command = new WebDavMoveCommand(userId, path ?? string.Empty, destination, overwrite);
        var result = await _mediator.Send(command);
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
        var destination = GetDestinationPath();
        var overwrite = GetOverwriteHeader();

        if (string.IsNullOrEmpty(destination))
        {
            return BadRequest("Destination header is required");
        }

        var ifHeader = Request.Headers["If"].FirstOrDefault();
        if (!_lockGuard.TryAuthorizeWrite(path ?? string.Empty, userId, ifHeader, out _)
            || !_lockGuard.TryAuthorizeWrite(destination, userId, ifHeader, out _))
        {
            AddDavHeaders();
            return StatusCode(StatusCodes.Status423Locked);
        }

        var command = new WebDavCopyCommand(userId, path ?? string.Empty, destination, overwrite);
        var result = await _mediator.Send(command);
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
            "OPTIONS", "PROPFIND", "GET", "HEAD", "PUT", "DELETE", "MKCOL", "MOVE", "COPY", "LOCK", "UNLOCK"
        ];

        var excludeSet = new HashSet<string>(exclude, StringComparer.OrdinalIgnoreCase);
        Response.Headers["DAV"] = "1,2";
        Response.Headers["MS-Author-Via"] = "DAV";
        Response.Headers.Allow = string.Join(", ",
            methods.Where(m => !excludeSet.Contains(m)));
    }

    private int GetDepthHeader()
    {
        var depthHeader = Request.Headers["Depth"].FirstOrDefault();
        return depthHeader switch
        {
            "0" => 0,
            "infinity" => int.MaxValue,
            _ => 1
        };
    }

    private string? GetDestinationPath()
    {
        var destination = Request.Headers["Destination"].FirstOrDefault();
        if (string.IsNullOrEmpty(destination))
        {
            return null;
        }

        // Parse the destination URL and extract the path
        if (Uri.TryCreate(destination, UriKind.Absolute, out var uri))
        {
            destination = uri.AbsolutePath;
        }

        // Remove the WebDAV route prefix
        var webdavIndex = destination.IndexOf(WebDavRoute, StringComparison.OrdinalIgnoreCase);
        if (webdavIndex >= 0)
        {
            destination = destination[(webdavIndex + WebDavRoute.Length)..];
        }

        return destination.Trim('/');
    }

    private bool GetOverwriteHeader()
    {
        var overwrite = Request.Headers["Overwrite"].FirstOrDefault();
        return !string.Equals(overwrite, "F", StringComparison.OrdinalIgnoreCase);
    }

    private TimeSpan? GetTimeoutHeader()
    {
        var timeoutHeader = Request.Headers["Timeout"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(timeoutHeader))
        {
            return TimeSpan.FromHours(1);
        }

        // Only support Second-N for now, fallback to 1 hour.
        if (timeoutHeader.StartsWith("Second-", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(timeoutHeader[7..], out var seconds)
            && seconds > 0)
        {
            // Clamp to 24h to avoid unbounded locks.
            seconds = Math.Min(seconds, 60 * 60 * 24);
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.FromHours(1);
    }
}