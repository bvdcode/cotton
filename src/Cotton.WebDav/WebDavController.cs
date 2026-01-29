using Microsoft.AspNetCore.Mvc;

namespace Cotton.WebDav
{
    [ApiController]
    [Route("api/v1/webdav")]
    public class WebDavController : ControllerBase
    {
        // потом сюда воткнём резолвер путей/хранилище через DI
        // private readonly IWebDavPathResolver _resolver;
        // private readonly IFileStorage _storage;
        // ...

        // public WebDavController(IWebDavPathResolver resolver, IFileStorage storage)
        // {
        //     _resolver = resolver;
        //     _storage = storage;
        // }

        [HttpOptions]
        public IActionResult HandleOptions()
        {
            Response.Headers["DAV"] = "1,2";
            Response.Headers["MS-Author-Via"] = "DAV";
            Response.Headers.Allow = "OPTIONS, PROPFIND, GET, HEAD";
            return Ok();
        }

        [AcceptVerbs("PROPFIND")]
        public Task<IActionResult> HandlePropFindAsync(string? path)
        {
            // тут ПОКА заглушка, но уже корректный 501 под WebDAV
            // позже сюда прикрутим резолвер пути и XML-ответ
            return Task.FromResult<IActionResult>(
                StatusCode(StatusCodes.Status501NotImplemented)
            );
        }

        [HttpGet]
        public Task<IActionResult> HandleGetAsync(string? path)
        {
            // заглушка — потом здесь будет стриминг из твоего стора
            return Task.FromResult<IActionResult>(
                StatusCode(StatusCodes.Status501NotImplemented)
            );
        }

        [HttpHead]
        public Task<IActionResult> HandleHeadAsync(string? path)
        {
            // можно просто переиспользовать GET-логику без тела, но пока заглушка
            return Task.FromResult<IActionResult>(
                StatusCode(StatusCodes.Status501NotImplemented)
            );
        }
    }
}