// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Previews;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Validators;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Extensions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    public class UpdateCurrentUserRequest(
        Guid userId,
        string? email,
        string? username,
        string? firstName,
        string? lastName,
        DateOnly? birthDate,
        string? avatarHash) : IRequest<UserDto>
    {
        public Guid UserId { get; } = userId;
        public string? Email { get; } = email;
        public string? Username { get; } = username;
        public string? FirstName { get; } = firstName;
        public string? LastName { get; } = lastName;
        public DateOnly? BirthDate { get; } = birthDate;
        public string? AvatarHash { get; } = avatarHash;
    }

    public class UpdateCurrentUserRequestHandler(
        CottonDbContext _dbContext,
        IStreamCipher _crypto,
        IStoragePipeline _storage,
        IChunkIngestService _chunkIngest) : IRequestHandler<UpdateCurrentUserRequest, UserDto>
    {
        private static readonly ImagePreviewGenerator _avatarGenerator = new();

        public async Task<UserDto> Handle(UpdateCurrentUserRequest request, CancellationToken cancellationToken)
        {
            User user = await LoadUserAsync(request.UserId, cancellationToken);

            string? newEmail = NormalizeEmail(request.Email);
            bool emailChanged = IsEmailChanged(user, newEmail);
            await EnsureEmailAvailableAsync(user, newEmail, emailChanged, cancellationToken);

            var usernameUpdate = await ResolveUsernameUpdateAsync(user, request.Username, cancellationToken);
            var avatarUpdate = await ResolveAvatarUpdateAsync(user, request.AvatarHash, cancellationToken);

            ApplyProfileFields(user, request);
            ApplyEmailUpdate(user, newEmail, emailChanged);
            ApplyUsernameUpdate(user, usernameUpdate);
            ApplyAvatarUpdate(user, avatarUpdate);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return user.Adapt<UserDto>();
        }

        private async Task<User> LoadUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            return await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                    ?? throw new EntityNotFoundException<User>();
        }

        private static string? NormalizeEmail(string? email)
        {
            return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        }

        private static bool IsEmailChanged(User user, string? newEmail)
        {
            return !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase);
        }

        private async Task EnsureEmailAvailableAsync(
            User user,
            string? newEmail,
            bool emailChanged,
            CancellationToken cancellationToken)
        {
            if (!emailChanged || newEmail is null)
            {
                return;
            }

            bool emailTaken = await _dbContext.Users
                .AnyAsync(x => x.Id != user.Id && x.Email == newEmail, cancellationToken);
            if (emailTaken)
            {
                throw new BadRequestException<User>("Email is already taken");
            }
        }

        private async Task<UsernameUpdate> ResolveUsernameUpdateAsync(
            User user,
            string? username,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new UsernameUpdate(null, false);
            }

            bool isValid = UsernameValidator.TryNormalizeAndValidate(username, out string normalizedUsername, out string error);
            if (!isValid)
            {
                throw new BadRequestException<User>(error);
            }

            bool usernameChanged = !string.Equals(user.Username, normalizedUsername, StringComparison.Ordinal);
            await EnsureUsernameAvailableAsync(user, normalizedUsername, usernameChanged, cancellationToken);
            return new UsernameUpdate(normalizedUsername, usernameChanged);
        }

        private async Task EnsureUsernameAvailableAsync(
            User user,
            string normalizedUsername,
            bool usernameChanged,
            CancellationToken cancellationToken)
        {
            if (!usernameChanged)
            {
                return;
            }

            bool usernameTaken = await _dbContext.Users
                .AnyAsync(x => x.Id != user.Id && x.Username == normalizedUsername, cancellationToken);
            if (usernameTaken)
            {
                throw new BadRequestException<User>("Username is already taken");
            }
        }

        private async Task<AvatarUpdate> ResolveAvatarUpdateAsync(
            User user,
            string? avatarHash,
            CancellationToken cancellationToken)
        {
            if (avatarHash is null)
            {
                return new AvatarUpdate(null, false);
            }

            if (string.IsNullOrWhiteSpace(avatarHash))
            {
                bool removeExistingAvatar = user.AvatarHash is not null || user.AvatarHashEncrypted is not null;
                return new AvatarUpdate(null, removeExistingAvatar);
            }

            byte[] avatarHashBytes = await CreateAvatarPreviewChunkAsync(user.Id, avatarHash, cancellationToken);
            bool changed = user.AvatarHash is null || !user.AvatarHash.SequenceEqual(avatarHashBytes);
            return new AvatarUpdate(avatarHashBytes, changed);
        }

        private async Task<byte[]> CreateAvatarPreviewChunkAsync(
            Guid userId,
            string sourceAvatarHash,
            CancellationToken cancellationToken)
        {
            byte[] sourceAvatarHashBytes = ParseAvatarHash(sourceAvatarHash);
            await EnsureAvatarChunkReadableAsync(userId, sourceAvatarHashBytes, cancellationToken);

            byte[] avatarPreviewWebP = await GenerateAvatarPreviewAsync(sourceAvatarHashBytes);
            Chunk avatarChunk = await _chunkIngest.UpsertChunkAsync(
                userId,
                avatarPreviewWebP,
                avatarPreviewWebP.Length,
                cancellationToken);
            return avatarChunk.Hash;
        }

        private static byte[] ParseAvatarHash(string avatarHash)
        {
            try
            {
                return Hasher.FromHexStringHash(avatarHash);
            }
            catch (ArgumentException)
            {
                throw new BadRequestException<User>("Invalid avatar hash format");
            }
        }

        private async Task EnsureAvatarChunkReadableAsync(
            Guid userId,
            byte[] sourceAvatarHashBytes,
            CancellationToken cancellationToken)
        {
            bool hasChunkOwnership = await _dbContext.ChunkOwnerships
                .AnyAsync(x => x.OwnerId == userId && x.ChunkHash == sourceAvatarHashBytes, cancellationToken);
            if (!hasChunkOwnership)
            {
                throw new BadRequestException<User>("Avatar chunk not found or not owned by user");
            }

            string sourceAvatarHash = Hasher.ToHexStringHash(sourceAvatarHashBytes);
            bool sourceChunkExists = await _storage.ExistsAsync(sourceAvatarHash);
            if (!sourceChunkExists)
            {
                throw new BadRequestException<User>("Avatar chunk not found");
            }
        }

        private async Task<byte[]> GenerateAvatarPreviewAsync(byte[] sourceAvatarHashBytes)
        {
            try
            {
                string sourceAvatarHash = Hasher.ToHexStringHash(sourceAvatarHashBytes);
                await using Stream sourceAvatarStream = await _storage.ReadAsync(sourceAvatarHash);
                return await _avatarGenerator.GeneratePreviewWebPAsync(
                    sourceAvatarStream,
                    PreviewGeneratorProvider.DefaultSmallPreviewSize);
            }
            catch (Exception)
            {
                throw new BadRequestException<User>("Avatar source must be a valid image");
            }
        }

        private static void ApplyProfileFields(User user, UpdateCurrentUserRequest request)
        {
            user.FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName.Trim();
            user.LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();
            user.BirthDate = request.BirthDate;
        }

        private static void ApplyEmailUpdate(User user, string? newEmail, bool emailChanged)
        {
            if (!emailChanged)
            {
                return;
            }

            user.Email = newEmail;
            user.IsEmailVerified = false;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenSentAt = null;
        }

        private static void ApplyUsernameUpdate(User user, UsernameUpdate update)
        {
            if (update.Changed)
            {
                user.Username = update.NormalizedUsername!;
            }
        }

        private void ApplyAvatarUpdate(User user, AvatarUpdate update)
        {
            if (!update.Changed)
            {
                return;
            }

            user.AvatarHash = update.Hash;
            user.AvatarHashEncrypted = update.Hash is null
                ? null
                : _crypto.Encrypt(update.Hash);
        }

        private sealed record UsernameUpdate(string? NormalizedUsername, bool Changed);

        private sealed record AvatarUpdate(byte[]? Hash, bool Changed);
    }
}
