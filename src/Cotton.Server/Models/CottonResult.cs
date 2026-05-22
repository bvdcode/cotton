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
        /// Gets or sets the success.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the response message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the data.
        /// </summary>
        public object? Data { get; set; }
        /// <summary>
        /// Gets or sets the message code.
        /// </summary>
        public string? MessageCode { get; set; }
        /// <summary>
        /// Gets or sets the status code.
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// Executes with message code.
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
