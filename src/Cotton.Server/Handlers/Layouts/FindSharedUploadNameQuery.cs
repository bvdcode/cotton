// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace Cotton.Server.Handlers.Layouts
{
    public record FindSharedUploadNameQuery(Guid NodeId, Guid OwnerId, string Name) : IRequest<string>;

    public class FindSharedUploadNameQueryHandler(CottonDbContext db)
        : IRequestHandler<FindSharedUploadNameQuery, string>
    {
        public async Task<string> Handle(FindSharedUploadNameQuery request, CancellationToken ct)
        {
            string candidate = request.Name;
            int number = 0;
            while (await ExistsAsync(request, candidate, ct))
            {
                candidate = AddSuffix(request.Name, ++number);
            }
            return candidate;
        }

        private async Task<bool> ExistsAsync(FindSharedUploadNameQuery request, string name, CancellationToken ct)
        {
            string key = NameValidator.NormalizeAndGetNameKey(name);
            return await db.NodeFiles.AnyAsync(file => file.NodeId == request.NodeId
                    && file.OwnerId == request.OwnerId && file.NameKey == key, ct)
                || await db.Nodes.AnyAsync(node => node.ParentId == request.NodeId
                    && node.OwnerId == request.OwnerId && node.Type == NodeType.Default && node.NameKey == key, ct);
        }

        private static string AddSuffix(string name, int number)
        {
            int dot = name.LastIndexOf('.');
            string stem = name;
            string extension = string.Empty;
            if (dot > 0)
            {
                stem = name[..dot];
                extension = name[dot..];
            }
            string suffix = string.Create(CultureInfo.InvariantCulture, $" ({number})");
            while (Encoding.UTF8.GetByteCount(stem + suffix + extension) > NameValidator.MaxSegmentBytes)
            {
                if (stem.Length > 0)
                {
                    stem = RemoveLastTextElement(stem);
                }
                else
                {
                    extension = RemoveLastTextElement(extension);
                }
            }
            return (stem + suffix + extension).TrimStart();
        }

        private static string RemoveLastTextElement(string value)
        {
            int[] elements = StringInfo.ParseCombiningCharacters(value);
            return value[..elements[^1]];
        }
    }
}
