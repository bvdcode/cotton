// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;
using Cotton.Server.Extensions;
using Cotton.TextExtraction;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class TextExtractorRegistrationTests
    {
        [TestCase("notes.txt")]
        [TestCase("README.md")]
        [TestCase("data.json")]
        [TestCase("page.html")]
        [TestCase("document.docx")]
        [TestCase("presentation.pptx")]
        [TestCase("workbook.xlsx")]
        [TestCase("book.epub")]
        [TestCase("message.eml")]
        [TestCase("document.odt")]
        [TestCase("workbook.ods")]
        [TestCase("presentation.odp")]
        [TestCase("calendar.ics")]
        [TestCase("contact.vcf")]
        [TestCase("notebook.ipynb")]
        [TestCase("application.log")]
        [TestCase("document.rst")]
        [TestCase("document.rest")]
        [TestCase("document.adoc")]
        [TestCase("document.asciidoc")]
        [TestCase("document.org")]
        [TestCase("paper.tex")]
        [TestCase("references.bib")]
        [TestCase("application.properties")]
        [TestCase("development.env")]
        public void RegisteredProviderSupportsResolvedFileType(string fileName)
        {
            ServiceCollection services = new();
            services.AddLogging();
            services.AddComputationServices();
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            FileTextExtractorProvider provider = serviceProvider.GetRequiredService<FileTextExtractorProvider>();

            string contentType = FileContentTypeResolver.ResolveFromFileName(fileName);

            Assert.That(provider.GetExtractor(contentType), Is.Not.Null, $"Resolved content type: {contentType}");
        }
    }
}
