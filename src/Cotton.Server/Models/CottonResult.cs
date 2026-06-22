// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Mime;

namespace Cotton.Server.Models
{
    /// <summary>
    /// Represents the result of cotton.
    /// </summary>
    public class CottonResult : IActionResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the human-readable response message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the payload returned to the client.
        /// </summary>
        public object? Data { get; set; }

        /// <summary>
        /// Gets or sets the machine-readable message code.
        /// </summary>
        public string? MessageCode { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code of the response.
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// Sets the message code and returns this instance.
        /// </summary>
        public CottonResult WithMessageCode(string messageCode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(messageCode);
            MessageCode = messageCode;
            return this;
        }

        /// <summary>
        /// Writes the typed HTTP result.
        /// </summary>
        public Task ExecuteResultAsync(ActionContext context)
        {
            var objectResult = new ObjectResult(this)
            {
                StatusCode = (int)StatusCode,
                ContentTypes = { MediaTypeNames.Application.Json }
            };
            return objectResult.ExecuteResultAsync(context);
        }

        /// <summary>
        /// Creates a bad request result.
        /// </summary>
        public static CottonResult BadRequest(string message)
        {
            return new()
            {
                Success = false,
                Message = message,
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        /// <summary>
        /// Creates an internal error result.
        /// </summary>
        public static CottonResult InternalError(string message)
        {
            return new()
            {
                Success = false,
                Message = message,
                StatusCode = HttpStatusCode.InternalServerError
            };
        }

        /// <summary>
        /// Creates a not-found result.
        /// </summary>
        public static CottonResult NotFound(string message)
        {
            return new()
            {
                Success = false,
                Message = message,
                StatusCode = HttpStatusCode.NotFound
            };
        }

        /// <summary>
        /// Creates a forbidden result.
        /// </summary>
        public static CottonResult Forbidden(string message)
        {
            return new()
            {
                Success = false,
                Message = message,
                StatusCode = HttpStatusCode.Forbidden
            };
        }
    }
}
