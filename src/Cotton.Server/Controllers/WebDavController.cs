using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Xml;

namespace Cotton.WebDav
{
    [ApiController]
    [Route("api/v1/webdav/{**path}")]
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
            Response.Headers["Allow"] = "OPTIONS, PROPFIND, GET, HEAD";
            return Ok();
        }

        [AcceptVerbs("PROPFIND")]
        public Task<IActionResult> HandlePropFindAsync(string? path)
        {
            // Нормализуем путь
            var cleanPath = (path ?? string.Empty).Trim('/');

            // Depth пока игнорим, просто возвращаем базовый набор
            var hrefBase = Url.Content("~/api/v1/webdav/") ?? "/api/v1/webdav/";

            string xml;

            if (string.IsNullOrEmpty(cleanPath))
            {
                // PROPFIND на корень: показываем корень + один файл hello.txt
                xml = BuildRootWithHelloResponse(hrefBase);
            }
            else if (string.Equals(cleanPath, "hello.txt", StringComparison.OrdinalIgnoreCase))
            {
                // PROPFIND на сам файл
                var href = hrefBase + "hello.txt";
                xml = BuildSingleFileResponse(href, "hello.txt");
            }
            else
            {
                // Ничего больше не знаем — 404
                return Task.FromResult<IActionResult>(NotFound());
            }

            var result = new ContentResult
            {
                StatusCode = 207,
                ContentType = "application/xml; charset=\"utf-8\"",
                Content = xml
            };

            return Task.FromResult<IActionResult>(result);
        }

        [HttpGet]
        public Task<IActionResult> HandleGetAsync(string? path)
        {
            var cleanPath = (path ?? string.Empty).Trim('/');
            if (!string.Equals(cleanPath, "hello.txt", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IActionResult>(NotFound());
            }
            const string content = "Hello from Cotton WebDAV!\n";
            return Task.FromResult<IActionResult>(
                Content(content, "text/plain", Encoding.UTF8)
            );
        }

        [HttpHead]
        public Task<IActionResult> HandleHeadAsync(string? path)
        {
            var cleanPath = (path ?? string.Empty).Trim('/');
            if (!string.Equals(cleanPath, "hello.txt", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IActionResult>(NotFound());
            }
            const string content = "Hello from Cotton WebDAV!\n";
            var bytes = Encoding.UTF8.GetByteCount(content);
            Response.ContentType = "text/plain; charset=utf-8";
            Response.ContentLength = bytes;
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

        private static string EnsureTrailingSlash(string href) =>
            href.EndsWith('/') ? href : href + "/";
    }
}