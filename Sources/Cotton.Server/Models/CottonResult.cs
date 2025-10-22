using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Models
{
    public class CottonResult : IActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public string? MessageCode { get; set; }
        public HttpStatusCode StatusCode { get; set; }

        public CottonResult WithMessageCode(string messageCode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(messageCode);
            MessageCode = messageCode;
            return this;
        }

        public Task ExecuteResultAsync(ActionContext context)
        {
            var objectResult = new ObjectResult(this)
            {
                StatusCode = (int)StatusCode,
                ContentTypes = { MediaTypeNames.Application.Json }
            };
            return objectResult.ExecuteResultAsync(context);
        }

        public static CottonResult BadRequest(string message)
        {
            return new()
            {
                Success = false,
                Message = message,
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        public static CottonResult InternalError(string message)
        {
            return new()
            {
                Success = false,
                Message = message,
                StatusCode = HttpStatusCode.InternalServerError
            };
        }

        public static CottonResult NotFound(string message)
        {
            return new()
            {
                Success = false,
                Message = message,
                StatusCode = HttpStatusCode.NotFound
            };
        }

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
