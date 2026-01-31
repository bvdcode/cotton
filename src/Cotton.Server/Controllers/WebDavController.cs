// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Handlers.WebDav;
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
    ILogger<WebDavController> _logger) : ControllerBase
{
    private const string WebDavRoute = "/api/v1/webdav/";

    [HttpOptions]
    [AllowAnonymous]
    public IActionResult HandleOptions()
    {
        AddDavHeaders();
        _logger.LogDebug("WebDAV OPTIONS, ip: {Ip}", Request.GetRemoteAddress());
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
        var result = await _mediator.Send(query);

        if (!result.Found)
        {
            return NotFound();
        }

        AddDavHeaders();
        return new ContentResult
        {
            StatusCode = 207,
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
        var result = await _mediator.Send(query);

        if (!result.Found)
        {
            return NotFound();
        }

        if (result.IsCollection)
        {
            // Return 200 OK for collections (some clients expect this)
            AddDavHeaders();
            return Ok();
        }

        AddDavHeaders();
        Response.Headers.ContentEncoding = "identity";
        Response.Headers.CacheControl = "private, no-store, no-transform";

        var entityTag = result.ETag is not null
            ? EntityTagHeaderValue.Parse(result.ETag)
            : EntityTagHeaderValue.Any;

        return File(
            result.Content!,
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

        _logger.LogDebug("WebDAV HEAD: {Path}, user: {UserId}, ip: {Ip}",
            path ?? "/", userId, Request.GetRemoteAddress());

        var query = new WebDavHeadQuery(userId, path ?? string.Empty);
        var result = await _mediator.Send(query);

        if (!result.Found)
        {
            return NotFound();
        }

        AddDavHeaders();
        Response.ContentType = result.ContentType ?? "application/octet-stream";
        Response.ContentLength = result.ContentLength;

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

        _logger.LogDebug("WebDAV PUT: {Path}, user: {UserId}, ip: {Ip}",
            path ?? "/", userId, Request.GetRemoteAddress());

        var contentType = Request.ContentType;
        var command = new WebDavPutFileCommand(
            userId,
            path ?? string.Empty,
            Request.Body,
            contentType);

        var result = await _mediator.Send(command);

        AddDavHeaders();

        if (!result.Success)
        {
            return result.Error switch
            {
                WebDavPutFileError.ParentNotFound => Conflict("Parent collection not found"),
                WebDavPutFileError.IsCollection => Conflict("Cannot PUT to a collection"),
                WebDavPutFileError.InvalidName => BadRequest("Invalid resource name"),
                WebDavPutFileError.Conflict => Conflict("Conflict with existing resource"),
                _ => StatusCode(500)
            };
        }

        return result.Created ? Created() : NoContent();
    }

    [HttpDelete]
    [Authorize(Policy = WebDavBasicAuthenticationHandler.PolicyName)]
    public async Task<IActionResult> HandleDeleteAsync(string? path)
    {
        var userId = User.GetUserId();

        _logger.LogDebug("WebDAV DELETE: {Path}, user: {UserId}, ip: {Ip}",
            path ?? "/", userId, Request.GetRemoteAddress());

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

        _logger.LogDebug("WebDAV MKCOL: {Path}, user: {UserId}, ip: {Ip}",
            path ?? "/", userId, Request.GetRemoteAddress());

        var command = new WebDavMkColCommand(userId, path ?? string.Empty);
        var result = await _mediator.Send(command);

        AddDavHeaders();

        if (!result.Success)
        {
            return result.Error switch
            {
                WebDavMkColError.ParentNotFound => Conflict("Parent collection not found"),
                WebDavMkColError.AlreadyExists => StatusCode(405, "Collection already exists"),
                WebDavMkColError.InvalidName => BadRequest("Invalid collection name"),
                WebDavMkColError.Conflict => Conflict("Conflict with existing resource"),
                _ => StatusCode(500)
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

        _logger.LogDebug("WebDAV MOVE: {Source} -> {Dest}, overwrite: {Overwrite}, user: {UserId}, ip: {Ip}",
            path ?? "/", destination, overwrite, userId, Request.GetRemoteAddress());

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
                _ => StatusCode(500)
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

        _logger.LogDebug("WebDAV COPY: {Source} -> {Dest}, overwrite: {Overwrite}, user: {UserId}, ip: {Ip}",
            path ?? "/", destination, overwrite, userId, Request.GetRemoteAddress());

        var command = new WebDavCopyCommand(userId, path ?? string.Empty, destination, overwrite);
        var result = await _mediator.Send(command);

        AddDavHeaders();

        if (!result.Success)
        {
            return result.Error switch
            {
                WebDavCopyError.SourceNotFound => NotFound(),
                WebDavCopyError.DestinationParentNotFound => Conflict("Destination parent not found"),
                WebDavCopyError.DestinationExists => StatusCode(412, "Destination exists and Overwrite is false"),
                WebDavCopyError.InvalidName => BadRequest("Invalid resource name"),
                WebDavCopyError.CannotCopyRoot => Forbid(),
                _ => StatusCode(500)
            };
        }

        return result.Created ? Created() : NoContent();
    }

    private void AddDavHeaders()
    {
        Response.Headers["DAV"] = "1,2";
        Response.Headers["MS-Author-Via"] = "DAV";
        Response.Headers.Allow = "OPTIONS, PROPFIND, GET, HEAD, PUT, DELETE, MKCOL, MOVE, COPY";
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
}