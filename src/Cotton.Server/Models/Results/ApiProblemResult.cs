// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Mime;

namespace Cotton.Server.Models.Results
{
    public class ApiProblemResult(int statusCode, string detail, string code) : ActionResult
    {
        public override Task ExecuteResultAsync(ActionContext context)
        {
            ProblemDetails problem = new()
            {
                Status = statusCode,
                Title = ReasonPhrases.GetReasonPhrase(statusCode),
                Detail = detail,
                Instance = context.HttpContext.Request.Path,
            };
            problem.Extensions["code"] = code;
            if (!string.IsNullOrWhiteSpace(context.HttpContext.TraceIdentifier))
            {
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            }

            return new ObjectResult(problem)
            {
                StatusCode = statusCode,
                ContentTypes = { MediaTypeNames.Application.ProblemJson },
            }.ExecuteResultAsync(context);
        }
    }
}
