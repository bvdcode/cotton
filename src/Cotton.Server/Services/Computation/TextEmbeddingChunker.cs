// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Computation;
using System.Runtime.CompilerServices;
using System.Text;

namespace Cotton.Server.Services.Computation
{
    public class TextEmbeddingChunker(TeiClient client)
    {
        private const int MaxTokenizationCharacters = 32768;

        public async IAsyncEnumerable<TextEmbeddingChunk> SplitAsync(
            Uri url, string text, int maxTokens, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTokens);
            for (int offset = 0; offset < text.Length;)
            {
                int end = offset + Math.Min(MaxTokenizationCharacters, text.Length - offset);
                if (end < text.Length && char.IsHighSurrogate(text[end - 1]))
                {
                    end--;
                }
                Stack<string> pending = new();
                pending.Push(text[offset..end]);
                offset = end;
                while (pending.TryPop(out string? part))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(part))
                    {
                        continue;
                    }
                    TeiToken[] tokens = await client.TokenizeAsync(url, part, cancellationToken);
                    if (tokens.Length <= maxTokens)
                    {
                        yield return new TextEmbeddingChunk(part, tokens.Length);
                        continue;
                    }
                    int split = GetSplitPosition(part, tokens, maxTokens);
                    pending.Push(part[split..]);
                    pending.Push(part[..split]);
                }
            }
        }

        private static int GetSplitPosition(string text, TeiToken[] tokens, int maxTokens)
        {
            int contentTokens = maxTokens - tokens.Count(token => token.Special);
            if (contentTokens <= 0)
            {
                throw new ComputationException(ComputationError.InvalidInput);
            }
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            int split = 0;
            foreach (TeiToken token in tokens.Where(token => !token.Special).Take(contentTokens))
            {
                if (token.Start is not int start || token.Stop is not int stop
                    || start < 0 || stop < start || stop > bytes.Length)
                {
                    throw new ComputationException(ComputationError.InvalidResponse);
                }
                if (stop < bytes.Length)
                {
                    split = Math.Max(split, stop);
                }
            }
            while (split > 0 && (bytes[split] & 0xC0) == 0x80)
            {
                split--;
            }
            if (split == 0)
            {
                throw new ComputationException(ComputationError.InvalidInput);
            }
            return Encoding.UTF8.GetCharCount(bytes.AsSpan(0, split));
        }
    }
}
