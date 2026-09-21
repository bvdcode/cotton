// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public class JupyterNotebookTextExtractionTests
    {
        [Test]
        public async Task Extract_ReadsCellSourcesWithoutOutputsAndKeepsSourceOpen()
        {
            const string notebook = """
                {
                  "cells": [
                    { "cell_type": "markdown", "source": ["# Analysis\n", "Important result"] },
                    { "cell_type": "code", "source": "value = 42", "outputs": [{ "text": ["large output"] }] }
                  ],
                  "nbformat": 4,
                  "nbformat_minor": 5
                }
                """;
            using MemoryStream source = new(Encoding.UTF8.GetBytes(notebook));
            JupyterNotebookTextExtractor extractor = new(NullLogger<JupyterNotebookTextExtractor>.Instance);

            string text = await extractor.ExtractAsync(source);

            Assert.Multiple(() =>
            {
                Assert.That(text, Is.EqualTo($"# Analysis{Environment.NewLine}Important result{Environment.NewLine}value = 42"));
                Assert.That(text, Does.Not.Contain("large output"));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public void Extract_InvalidNotebookReportsUnreadableContent()
        {
            using MemoryStream source = new(Encoding.UTF8.GetBytes("{}"));
            JupyterNotebookTextExtractor extractor = new(NullLogger<JupyterNotebookTextExtractor>.Instance);

            Assert.ThrowsAsync<FileTextExtractionException>(async () => await extractor.ExtractAsync(source));
        }

        [Test]
        public void Extract_InvalidCellSourceReportsUnreadableContent()
        {
            using MemoryStream source = new(Encoding.UTF8.GetBytes("{\"cells\":[{\"source\":[42]}]}"));
            JupyterNotebookTextExtractor extractor = new(NullLogger<JupyterNotebookTextExtractor>.Instance);

            Assert.ThrowsAsync<FileTextExtractionException>(async () => await extractor.ExtractAsync(source));
        }
    }
}
