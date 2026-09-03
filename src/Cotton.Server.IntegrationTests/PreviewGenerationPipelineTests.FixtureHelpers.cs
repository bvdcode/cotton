// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
        private static string? ResolveContentType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".txt" => "text/plain",
                ".md" => "text/markdown",
                ".xml" => "application/xml",
                ".json" => "application/json",
                ".js" => "application/javascript",
                ".pdf" => "application/pdf",
                ".stl" => "model/stl",
                ".obj" => "model/obj",
                ".3mf" => "model/3mf",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".tif" => "image/tiff",
                ".tiff" => "image/tiff",
                ".heic" => "image/heic",
                ".heif" => "image/heif",
                ".mp3" => "audio/mpeg",
                ".flac" => "audio/flac",
                ".wav" => "audio/wav",
                ".aac" => "audio/aac",
                ".m4a" => "audio/x-m4a",
                ".ogg" => "audio/ogg",
                ".opus" => "audio/opus",
                ".aiff" => "audio/aiff",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mov" => "video/mov",
                ".mkv" => "video/mkv",
                ".avi" => "video/avi",
                _ => null
            };
        }

        private static string ResolveExternalFixturesDir()
        {
            string? fromEnvironment = Environment.GetEnvironmentVariable("COTTON_PREVIEW_FIXTURES_DIR");
            return string.IsNullOrWhiteSpace(fromEnvironment)
                ? DefaultExternalFixturesDir
                : fromEnvironment;
        }

        private static void EnsureDefaultFixturesExist(string fixturesDir)
        {
            if (Directory.EnumerateFiles(fixturesDir).Any())
            {
                return;
            }

            File.WriteAllText(
                Path.Combine(fixturesDir, "01_text.txt"),
                "Cotton preview fixture: plain text file for generator coverage.");

            File.WriteAllText(
                Path.Combine(fixturesDir, "02_markdown.md"),
                "# Cotton Preview Fixture\n\nThis file validates markdown preview rendering.");

            File.WriteAllText(
                Path.Combine(fixturesDir, "03_data.json"),
                "{\"name\":\"cotton\",\"kind\":\"preview-fixture\"}");

            File.WriteAllText(
                Path.Combine(fixturesDir, "04_data.xml"),
                "<root><name>cotton</name><kind>preview-fixture</kind></root>");

            File.WriteAllBytes(
                Path.Combine(fixturesDir, "05_image.png"),
                CreateGradientPngBytes(width: 1600, height: 900));

            File.WriteAllBytes(
                Path.Combine(fixturesDir, "06_document.pdf"),
                CreateSinglePagePdfBytes("Cotton preview fixture PDF"));
        }

        private static byte[] CreateGradientPngBytes(int width, int height)
        {
            using Image<Rgba32> image = new Image<Rgba32>(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte red = (byte)((x * 255) / Math.Max(1, width - 1));
                    byte green = (byte)((y * 255) / Math.Max(1, height - 1));
                    byte blue = (byte)((x + y) % 256);
                    image[x, y] = new Rgba32(red, green, blue, 255);
                }
            }

            using MemoryStream ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }

        private static byte[] CreateTruncatedPngBytes() =>
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01,
        ];

        private static async Task<byte[]> CreateAudioBytesAsync(
            string? title = null,
            string? artist = null,
            string? album = null)
        {
            await FfmpegBinary.EnsureAvailableAsync();
            ProcessStartInfo startInfo = new()
            {
                FileName = FfmpegBinary.GetFfmpegPath(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            string[] commonArguments =
            [
                "-hide_banner",
                "-loglevel",
                "error",
                "-f",
                "lavfi",
                "-i",
                "anullsrc=r=8000:cl=mono",
                "-t",
                "0.1"
            ];
            foreach (string argument in commonArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            AddMetadataArgument(startInfo, "title", title);
            AddMetadataArgument(startInfo, "artist", artist);
            AddMetadataArgument(startInfo, "album", album);
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("mp3");
            startInfo.ArgumentList.Add("pipe:1");

            using Process process = new() { StartInfo = startInfo };
            Assert.That(process.Start(), Is.True);
            using MemoryStream output = new();
            Task copyTask = process.StandardOutput.BaseStream.CopyToAsync(output);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(copyTask, process.WaitForExitAsync());
            string stderr = await stderrTask;
            Assert.That(process.ExitCode, Is.EqualTo(0), stderr);
            return output.ToArray();
        }

        private static void AddMetadataArgument(
            ProcessStartInfo startInfo,
            string key,
            string? value)
        {
            if (value is null)
            {
                return;
            }

            startInfo.ArgumentList.Add("-metadata");
            startInfo.ArgumentList.Add($"{key}={value}");
        }

        private static byte[] CreateSinglePagePdfBytes(string text)
        {
            string escaped = text
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);

            string content = $"BT /F1 24 Tf 50 140 Td ({escaped}) Tj ET";
            byte[] contentBytes = Encoding.ASCII.GetBytes(content);

            string[] objects =
            [
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
                $"<< /Length {contentBytes.Length} >>\nstream\n{content}\nendstream",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
            ];

            using MemoryStream ms = new MemoryStream();
            List<long> offsets = new List<long> { 0 };

            static void WriteAscii(MemoryStream stream, string value)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(value);
                stream.Write(bytes, 0, bytes.Length);
            }

            WriteAscii(ms, "%PDF-1.4\n");

            for (int i = 0; i < objects.Length; i++)
            {
                offsets.Add(ms.Position);
                WriteAscii(ms, $"{i + 1} 0 obj\n");
                WriteAscii(ms, objects[i]);
                WriteAscii(ms, "\nendobj\n");
            }

            long xrefOffset = ms.Position;

            WriteAscii(ms, $"xref\n0 {offsets.Count}\n");
            WriteAscii(ms, "0000000000 65535 f \n");
            for (int i = 1; i < offsets.Count; i++)
            {
                WriteAscii(ms, $"{offsets[i]:0000000000} 00000 n \n");
            }

            WriteAscii(ms, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
            return ms.ToArray();
        }

    }
}
