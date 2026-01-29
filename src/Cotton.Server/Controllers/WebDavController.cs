using EasyExtensions.AspNetCore.Extensions;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Xml;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route("api/v1/webdav/{**path}")]
    public class WebDavController(ILogger<WebDavController> _logger) : ControllerBase
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
            AddDavHeaders();
            _logger.LogInformation("Handled OPTIONS request for WebDAV, ip: {ip}", Request.GetRemoteAddress());
            return Ok();
        }

        [AcceptVerbs("PROPFIND")]
        public async Task HandlePropFindAsync(string? path)
        {
            _logger.LogInformation("Handled PROPFIND request for WebDAV, path: {path}, ip: {ip}",
                path ?? string.Empty,
                Request.GetRemoteAddress());

            // Нормализуем путь
            var cleanPath = (path ?? string.Empty).Trim('/');

            // Базовый href. Для простоты оставим относительным как сейчас.
            var hrefBase = Url.Content("~/api/v1/webdav/") ?? "/api/v1/webdav/";

            string xml;

            if (string.IsNullOrEmpty(cleanPath))
            {
                xml = BuildRootWithHelloResponse(hrefBase);
            }
            else if (string.Equals(cleanPath, "hello.txt", StringComparison.OrdinalIgnoreCase))
            {
                var href = hrefBase + "hello.txt";
                xml = BuildSingleFileResponse(href, "hello.txt");
            }
            else
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            // *** Ключевой момент: руками пишем тело и Content-Length ***
            var bytes = Encoding.UTF8.GetBytes(xml);

            Response.StatusCode = 207; // Multi-Status
            Response.ContentType = "application/xml; charset=\"utf-8\"";
            Response.ContentLength = bytes.Length;
            AddDavHeaders();

            await Response.Body.WriteAsync(bytes);
        }

        [HttpGet]
        public Task<IActionResult> HandleGetAsync(string? path)
        {
            _logger.LogInformation("Handled GET request for WebDAV, path: {path}, ip: {ip}",
                path ?? string.Empty,
                Request.GetRemoteAddress());
            var cleanPath = (path ?? string.Empty).Trim('/');
            if (!string.Equals(cleanPath, "hello.txt", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IActionResult>(NotFound());
            }
            const string content = "Hello from Cotton WebDAV!\n";
            AddDavHeaders();
            return Task.FromResult<IActionResult>(
                Content(content, "text/plain", Encoding.UTF8)
            );
        }

        [HttpHead]
        public Task<IActionResult> HandleHeadAsync(string? path)
        {
            _logger.LogInformation("Handled HEAD request for WebDAV, path: {path}, ip: {ip}",
                path ?? string.Empty,
                Request.GetRemoteAddress());
            var cleanPath = (path ?? string.Empty).Trim('/');
            if (!string.Equals(cleanPath, "hello.txt", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IActionResult>(NotFound());
            }
            const string content = "Hello from Cotton WebDAV!\n";
            var bytes = Encoding.UTF8.GetByteCount(content);
            Response.ContentType = "text/plain; charset=utf-8";
            Response.ContentLength = bytes;
            AddDavHeaders();
            return Task.FromResult<IActionResult>(Ok());
        }


        private static string BuildRootWithHelloResponse(string hrefBase)
        {
            var now = DateTimeOffset.UtcNow;

            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = false,
                Indent = true,
                Encoding = Encoding.UTF8
            };

            using var writer = XmlWriter.Create(sb, settings);

            writer.WriteStartDocument();
            writer.WriteStartElement("d", "multistatus", "DAV:");

            // 1) Корневая коллекция
            WriteCollectionResponse(
                writer,
                href: EnsureTrailingSlash(hrefBase),
                displayName: "root",
                lastModified: now,
                etag: "\"root-etag\""
            );

            // 2) Файл hello.txt
            WriteFileResponse(
                writer,
                href: hrefBase + "hello.txt",
                displayName: "hello.txt",
                contentLength: "26", // длина строки "Hello from Cotton WebDAV!\n"
                lastModified: now,
                etag: "\"hello-etag\""
            );

            writer.WriteEndElement(); // multistatus
            writer.WriteEndDocument();

            return sb.ToString();
        }

        private static string BuildSingleFileResponse(string href, string displayName)
        {
            var now = DateTimeOffset.UtcNow;

            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = false,
                Indent = true,
                Encoding = Encoding.UTF8
            };

            using var writer = XmlWriter.Create(sb, settings);

            writer.WriteStartDocument();
            writer.WriteStartElement("d", "multistatus", "DAV:");

            WriteFileResponse(
                writer,
                href: href,
                displayName: displayName,
                contentLength: "26",
                lastModified: now,
                etag: "\"hello-etag\""
            );

            writer.WriteEndElement(); // multistatus
            writer.WriteEndDocument();

            return sb.ToString();
        }

        private static void WriteCollectionResponse(
            XmlWriter writer,
            string href,
            string displayName,
            DateTimeOffset lastModified,
            string etag)
        {
            writer.WriteStartElement("d", "response", null);
            writer.WriteElementString("d", "href", null, href);

            writer.WriteStartElement("d", "propstat", null);
            writer.WriteStartElement("d", "prop", null);

            writer.WriteElementString("d", "displayname", null, displayName);

            writer.WriteStartElement("d", "resourcetype", null);
            writer.WriteElementString("d", "collection", null, string.Empty);
            writer.WriteEndElement(); // resourcetype

            writer.WriteElementString("d", "getcontentlength", null, "0");
            writer.WriteElementString("d", "getlastmodified", null, lastModified.ToString("R"));
            writer.WriteElementString("d", "getetag", null, etag);

            writer.WriteEndElement(); // prop
            writer.WriteElementString("d", "status", null, "HTTP/1.1 200 OK");
            writer.WriteEndElement(); // propstat

            writer.WriteEndElement(); // response
        }

        private static void WriteFileResponse(
            XmlWriter writer,
            string href,
            string displayName,
            string contentLength,
            DateTimeOffset lastModified,
            string etag)
        {
            writer.WriteStartElement("d", "response", null);
            writer.WriteElementString("d", "href", null, href);

            writer.WriteStartElement("d", "propstat", null);
            writer.WriteStartElement("d", "prop", null);

            writer.WriteElementString("d", "displayname", null, displayName);

            // Файл: resourcetype пустой
            writer.WriteStartElement("d", "resourcetype", null);
            writer.WriteEndElement(); // resourcetype

            writer.WriteElementString("d", "getcontentlength", null, contentLength);
            writer.WriteElementString("d", "getlastmodified", null, lastModified.ToString("R"));
            writer.WriteElementString("d", "getetag", null, etag);

            writer.WriteEndElement(); // prop
            writer.WriteElementString("d", "status", null, "HTTP/1.1 200 OK");
            writer.WriteEndElement(); // propstat

            writer.WriteEndElement(); // response
        }

        private void AddDavHeaders()
        {
            Response.Headers["DAV"] = "1,2";
            Response.Headers["MS-Author-Via"] = "DAV";
            Response.Headers.Allow = "OPTIONS, PROPFIND, GET, HEAD";
        }


        private static string EnsureTrailingSlash(string href) =>
            href.EndsWith('/') ? href : href + "/";
    }
}