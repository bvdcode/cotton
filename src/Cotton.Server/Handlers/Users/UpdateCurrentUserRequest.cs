// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

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
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
                    ?? throw new EntityNotFoundException<User>();

            string? newEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            bool emailChanged = !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase);

            string? newUsername = await ResolveNewUsernameAsync(user, request.Username, cancellationToken);
            bool usernameChanged = newUsername is not null;

            if (emailChanged && newEmail != null)
            {
                await EnsureEmailAvailableAsync(user.Id, newEmail, cancellationToken);
            }

            (byte[]? avatarHashBytes, bool avatarHashUpdated) = await ResolveAvatarAsync(user, request.AvatarHash, cancellationToken);

            user.FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName.Trim();
            user.LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();
            user.BirthDate = request.BirthDate;

            if (emailChanged)
            {
                user.Email = newEmail;
                user.IsEmailVerified = false;
                user.EmailVerificationToken = null;
                user.EmailVerificationTokenSentAt = null;
            }

            if (usernameChanged)
            {
                user.Username = newUsername!;
            }

            if (avatarHashUpdated)
            {
                user.AvatarHash = avatarHashBytes;
                user.AvatarHashEncrypted = avatarHashBytes is null
                    ? null
                    : _crypto.Encrypt(avatarHashBytes);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return user.Adapt<UserDto>();
        }

        private async Task<string?> ResolveNewUsernameAsync(User user, string? rawUsername, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(rawUsername))
            {
                return null;
            }

            bool isValid = UsernameValidator.TryNormalizeAndValidate(rawUsername, out string normalizedUsername, out string error);
            if (!isValid)
            {
                throw new BadRequestException<User>(error);
            }

            if (string.Equals(user.Username, normalizedUsername, StringComparison.Ordinal))
            {
                return null;
            }

            bool usernameTaken = await _dbContext.Users
                .AnyAsync(x => x.Id != user.Id && x.Username == normalizedUsername, cancellationToken);
            if (usernameTaken)
            {
                throw new BadRequestException<User>("Username is already taken");
            }

            return normalizedUsername;
        }

        private async Task EnsureEmailAvailableAsync(Guid userId, string newEmail, CancellationToken cancellationToken)
        {
            bool emailTaken = await _dbContext.Users
                .AnyAsync(x => x.Id != userId && x.Email == newEmail, cancellationToken);
            if (emailTaken)
            {
                throw new BadRequestException<User>("Email is already taken");
            }
        }

        private async Task<(byte[]? Hash, bool Updated)> ResolveAvatarAsync(User user, string? avatarHashInput, CancellationToken cancellationToken)
        {
            if (avatarHashInput is null)
            {
                return (null, false);
            }

            if (string.IsNullOrWhiteSpace(avatarHashInput))
            {
                bool clearedSomething = user.AvatarHash is not null || user.AvatarHashEncrypted is not null;
                return (null, clearedSomething);
            }

            byte[] sourceAvatarHashBytes = ParseAvatarHashOrThrow(avatarHashInput);
            await EnsureAvatarChunkOwnedAsync(user.Id, sourceAvatarHashBytes, cancellationToken);

            string sourceAvatarHash = Hasher.ToHexStringHash(sourceAvatarHashBytes);
            bool sourceChunkExists = await _storage.ExistsAsync(sourceAvatarHash);
            if (!sourceChunkExists)
            {
                throw new BadRequestException<User>("Avatar chunk not found");
            }

            byte[] avatarPreviewWebP = await RenderAvatarPreviewAsync(sourceAvatarHash);
            Chunk avatarChunk = await _chunkIngest.UpsertChunkAsync(user.Id, avatarPreviewWebP, avatarPreviewWebP.Length, cancellationToken);
            byte[] avatarHashBytes = avatarChunk.Hash;
            bool updated = user.AvatarHash is null || !user.AvatarHash.SequenceEqual(avatarHashBytes);
            return (avatarHashBytes, updated);
        }

        private static byte[] ParseAvatarHashOrThrow(string avatarHash)
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

        private async Task EnsureAvatarChunkOwnedAsync(Guid userId, byte[] chunkHash, CancellationToken cancellationToken)
        {
            bool hasChunkOwnership = await _dbContext.ChunkOwnerships
                .AnyAsync(x => x.OwnerId == userId && x.ChunkHash == chunkHash, cancellationToken);
            if (!hasChunkOwnership)
            {
                throw new BadRequestException<User>("Avatar chunk not found or not owned by user");
            }
        }

        private async Task<byte[]> RenderAvatarPreviewAsync(string sourceAvatarHash)
        {
            try
            {
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
    }
}
