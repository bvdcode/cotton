// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Text;
using System.Xml;

namespace Cotton.Server.Services.WebDav
{
    public static class WebDavXmlBuilder
    {
        private const string DavNamespace = "DAV:";

        public static string BuildMultiStatusResponse(IEnumerable<WebDavResource> resources)
        {
            StringBuilder sb = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true, // Windows WebDAV client doesn't like encoding declaration
                Indent = false,
                Encoding = Encoding.UTF8
            };

            using StringWriter stringWriter = new StringWriter(sb);
            using (XmlWriter writer = XmlWriter.Create(stringWriter, settings))
            {
                writer.WriteStartElement("d", "multistatus", DavNamespace);

                foreach (WebDavResource resource in resources)
                {
                    WriteResourceResponse(writer, resource);
                }

                writer.WriteEndElement(); // multistatus
            }

            return sb.ToString();
        }

        public static string BuildPropPatchOkResponse(string href)
        {
            StringBuilder sb = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false,
                Encoding = Encoding.UTF8
            };

            using StringWriter stringWriter = new StringWriter(sb);
            using (XmlWriter writer = XmlWriter.Create(stringWriter, settings))
            {
                writer.WriteStartElement("d", "multistatus", DavNamespace);

                writer.WriteStartElement("d", "response", DavNamespace);
                writer.WriteElementString("d", "href", DavNamespace, href);

                writer.WriteStartElement("d", "propstat", DavNamespace);
                writer.WriteStartElement("d", "prop", DavNamespace);
                writer.WriteEndElement(); // prop
                writer.WriteElementString("d", "status", DavNamespace, "HTTP/1.1 200 OK");
                writer.WriteEndElement(); // propstat

                writer.WriteEndElement(); // response
                writer.WriteEndElement(); // multistatus
            }

            return sb.ToString();
        }

        public static string BuildLockDiscoveryResponse(string token, TimeSpan timeout)
        {
            StringBuilder sb = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false,
                Encoding = Encoding.UTF8
            };

            using StringWriter stringWriter = new StringWriter(sb);
            using (XmlWriter writer = XmlWriter.Create(stringWriter, settings))
            {
                writer.WriteStartElement("d", "prop", DavNamespace);

                writer.WriteStartElement("d", "lockdiscovery", DavNamespace);
                writer.WriteStartElement("d", "activelock", DavNamespace);

                writer.WriteStartElement("d", "locktype", DavNamespace);
                writer.WriteElementString("d", "write", DavNamespace, string.Empty);
                writer.WriteEndElement();

                writer.WriteStartElement("d", "lockscope", DavNamespace);
                writer.WriteElementString("d", "exclusive", DavNamespace, string.Empty);
                writer.WriteEndElement();

                writer.WriteElementString("d", "depth", DavNamespace, "Infinity");
                writer.WriteElementString("d", "timeout", DavNamespace, $"Second-{(int)timeout.TotalSeconds}");

                writer.WriteStartElement("d", "locktoken", DavNamespace);
                writer.WriteElementString("d", "href", DavNamespace, token);
                writer.WriteEndElement();

                writer.WriteEndElement(); // activelock
                writer.WriteEndElement(); // lockdiscovery
                writer.WriteEndElement(); // prop
            }

            return sb.ToString();
        }

        private static void WriteResourceResponse(XmlWriter writer, WebDavResource resource)
        {
            writer.WriteStartElement("d", "response", DavNamespace);
            writer.WriteElementString("d", "href", DavNamespace, resource.Href);

            writer.WriteStartElement("d", "propstat", DavNamespace);
            writer.WriteStartElement("d", "prop", DavNamespace);

            writer.WriteElementString("d", "displayname", DavNamespace, resource.DisplayName);

            writer.WriteStartElement("d", "resourcetype", DavNamespace);
            if (resource.IsCollection)
            {
                writer.WriteElementString("d", "collection", DavNamespace, string.Empty);
            }
            writer.WriteEndElement(); // resourcetype

            writer.WriteElementString("d", "getcontentlength", DavNamespace, resource.ContentLength.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("d", "getlastmodified", DavNamespace, resource.LastModified.ToString("R"));
            writer.WriteElementString("d", "getetag", DavNamespace, resource.ETag);

            if (resource.Quota is not null)
            {
                writer.WriteElementString(
                    "d",
                    "quota-used-bytes",
                    DavNamespace,
                    resource.Quota.UsedBytes.ToString(CultureInfo.InvariantCulture));

                if (resource.Quota.AvailableBytes is long availableBytes)
                {
                    writer.WriteElementString(
                        "d",
                        "quota-available-bytes",
                        DavNamespace,
                        availableBytes.ToString(CultureInfo.InvariantCulture));
                }
            }

            if (!resource.IsCollection && !string.IsNullOrEmpty(resource.ContentType))
            {
                writer.WriteElementString("d", "getcontenttype", DavNamespace, resource.ContentType);
            }

            writer.WriteEndElement(); // prop
            writer.WriteElementString("d", "status", DavNamespace, "HTTP/1.1 200 OK");
            writer.WriteEndElement(); // propstat

            writer.WriteEndElement(); // response
        }
    }
}
