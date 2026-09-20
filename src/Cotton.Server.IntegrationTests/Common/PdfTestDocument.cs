// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text;

namespace Cotton.Server.IntegrationTests.Common
{
    internal static class PdfTestDocument
    {
        public static byte[] Create(params string[] pages)
        {
            string children = string.Join(" ", Enumerable.Range(0, pages.Length).Select(index => $"{4 + index * 2} 0 R"));
            List<string> objects =
            [
                "<< /Type /Catalog /Pages 2 0 R >>",
                $"<< /Type /Pages /Count {pages.Length} /Kids [{children}] >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            ];
            foreach (string page in pages)
            {
                string escaped = page.Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
                string content = $"BT /F1 12 Tf 20 140 Td ({escaped}) Tj ET";
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 600 300] /Contents {objects.Count + 2} 0 R /Resources << /Font << /F1 3 0 R >> >> >>");
                objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
            }
            using MemoryStream stream = new();
            List<long> offsets = [0];
            Write("%PDF-1.4\n");
            foreach (string value in objects)
            {
                offsets.Add(stream.Position);
                Write($"{offsets.Count - 1} 0 obj\n{value}\nendobj\n");
            }
            long xrefOffset = stream.Position;
            Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
            foreach (long offset in offsets.Skip(1))
            {
                Write($"{offset:0000000000} 00000 n \n");
            }
            Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
            return stream.ToArray();

            void Write(string value) => stream.Write(Encoding.ASCII.GetBytes(value));
        }
    }
}
