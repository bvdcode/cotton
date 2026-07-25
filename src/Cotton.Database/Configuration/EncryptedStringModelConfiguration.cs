// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database.Models.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Security.Cryptography;

namespace Cotton.Database.Configuration
{
    internal static class EncryptedStringModelConfiguration
    {
        public static void Configure(
            ModelBuilder modelBuilder,
            IStreamCipher? streamCipher,
            ILogger? logger)
        {
            ValueConverter<string?, string?> converter = new(
                value => Encrypt(value, streamCipher),
                value => Decrypt(value, streamCipher, logger));

            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                ConfigureEntity(entityType, modelBuilder, converter);
            }
        }

        private static void ConfigureEntity(
            IMutableEntityType entityType,
            ModelBuilder modelBuilder,
            ValueConverter<string?, string?> converter)
        {
            Type clrType = entityType.ClrType;
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                PropertyInfo? propertyInfo = property.PropertyInfo;
                if (propertyInfo is null
                    || property.ClrType != typeof(string)
                    || !Attribute.IsDefined(propertyInfo, typeof(EncryptedAttribute)))
                {
                    continue;
                }

                modelBuilder.Entity(clrType)
                    .Property(propertyInfo.Name)
                    .HasConversion(converter);
            }
        }

        private static string? Encrypt(string? value, IStreamCipher? streamCipher)
        {
            if (value is null)
            {
                return null;
            }

            if (streamCipher is null)
            {
                throw CreateMissingStreamCipherException();
            }

            byte[] encryptedBytes = streamCipher.EncryptString(value);
            return Convert.ToBase64String(encryptedBytes);
        }

        private static string? Decrypt(
            string? value,
            IStreamCipher? streamCipher,
            ILogger? logger)
        {
            if (value is null)
            {
                return null;
            }

            if (streamCipher is null)
            {
                throw CreateMissingStreamCipherException();
            }

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(value);
                return streamCipher.DecryptString(encryptedBytes);
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException or InvalidDataException)
            {
                logger?.LogError(ex, "Failed to decrypt value in encrypted EF converter.");
                throw;
            }
        }

        private static InvalidOperationException CreateMissingStreamCipherException()
        {
            return new InvalidOperationException(
                "Encrypted EF string conversion requires IStreamCipher. Use a raw startup/probe DbContext for pre-unlock reads.");
        }
    }
}
