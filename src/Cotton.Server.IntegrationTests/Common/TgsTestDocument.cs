// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.IO.Compression;
using System.Text;

namespace Cotton.Server.IntegrationTests.Common
{
    public static class TgsTestDocument
    {
        private const string AnimationJson = """
            {
              "v": "5.5.2", "fr": 60, "ip": 0, "op": 60, "w": 512, "h": 512,
              "nm": "Стикер", "assets": [],
              "layers": [{
                "ddd": 0, "ind": 1, "ty": 1, "nm": "red", "sr": 1,
                "ks": {
                  "o": {"a": 0, "k": 100}, "r": {"a": 0, "k": 0},
                  "p": {"a": 0, "k": [256, 256, 0]},
                  "a": {"a": 0, "k": [256, 256, 0]},
                  "s": {"a": 0, "k": [100, 100, 100]}
                },
                "ao": 0, "sw": 512, "sh": 512, "sc": "#ff0000",
                "ip": 0, "op": 60, "st": 0, "bm": 0
              }]
            }
            """;

        public static async Task<byte[]> CreateAsync(string? json = null)
        {
            using MemoryStream output = new();
            await using (GZipStream gzip = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                await gzip.WriteAsync(Encoding.UTF8.GetBytes(json ?? AnimationJson));
            }
            return output.ToArray();
        }
    }
}
