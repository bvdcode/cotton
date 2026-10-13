// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.DatabaseBackup;
using Cotton.Server.Services.Search;
using System.Text;

namespace Cotton.Server.Services
{
    internal record PostgresRestoreList(string Content, PostgresRestoreResult Result)
    {
        public static PostgresRestoreList Parse(string tableOfContents)
        {
            StringBuilder content = new();
            bool hadVectorExtension = false;
            bool hadVectorIndex = false;
            foreach (string line in tableOfContents.Split('\n'))
            {
                if (line.TrimStart().StartsWith(';'))
                {
                    content.AppendLine(line.TrimEnd('\r'));
                    continue;
                }
                string[] parts = line.Split([' ', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries);
                bool vectorExtension = parts.Length >= 6 && parts[3] == "EXTENSION"
                    && parts[4] == "-" && parts[5] == VectorIndexDefinition.ExtensionName;
                bool vectorComment = parts.Length >= 7 && parts[3] == "COMMENT"
                    && parts[4] == "-" && parts[5] == "EXTENSION"
                    && parts[6] == VectorIndexDefinition.ExtensionName;
                bool vectorIndex = parts.Length >= 6 && parts[3] == "INDEX"
                    && parts[4] == VectorIndexDefinition.Expected.SchemaName
                    && parts[5] == VectorIndexDefinition.Expected.IndexName;
                bool vectorIndexComment = parts.Length >= 7 && parts[3] == "COMMENT"
                    && parts[4] == VectorIndexDefinition.Expected.SchemaName && parts[5] == "INDEX"
                    && parts[6] == VectorIndexDefinition.Expected.IndexName;
                hadVectorExtension |= vectorExtension;
                hadVectorIndex |= vectorIndex;
                if (!vectorExtension && !vectorComment && !vectorIndex && !vectorIndexComment)
                {
                    content.AppendLine(line.TrimEnd('\r'));
                }
            }
            return new(content.ToString(), new(hadVectorExtension, hadVectorIndex));
        }
    }
}
