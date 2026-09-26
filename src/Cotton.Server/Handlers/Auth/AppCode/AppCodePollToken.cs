// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.Handlers.Auth.AppCode
{
    internal static class AppCodePollToken
    {
        private const int SecretByteLength = 32;
        private const int SecretLength = SecretByteLength * 2;

        public static (string Token, byte[] SecretHash) Create(Guid approvalId)
        {
            string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretByteLength)).ToLowerInvariant();
            return ($"{approvalId:D}.{secret}", Hash(secret));
        }

        public static (Guid ApprovalId, string Secret) Parse(string? value)
        {
            string? normalized = value?.Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                throw new BadRequestException<AppCodePollRequestDto>("PollToken is required.");
            }

            string[] parts = normalized.Split('.', 2);
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out Guid approvalId)
                || parts[1].Length != SecretLength)
            {
                throw new BadRequestException<AppCodePollRequestDto>("PollToken is invalid.");
            }

            return (approvalId, parts[1]);
        }

        public static bool IsValid(AppCodeRequestState state, string secret)
        {
            return CryptographicOperations.FixedTimeEquals(state.PollSecretHash, Hash(secret));
        }

        private static byte[] Hash(string secret)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        }
    }
}
