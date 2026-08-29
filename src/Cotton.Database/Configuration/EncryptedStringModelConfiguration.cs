// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Linq.Expressions;

namespace Cotton.Database.Configuration
{
    internal static class EncryptedStringModelConfiguration
    {
        public static void Configure(
            ModelBuilder modelBuilder,
            IDatabaseFieldProtector? databaseFieldProtector)
        {
            ValueConverter<string?, string?> converter = new(
                value => Protect(value, databaseFieldProtector),
                value => Unprotect(value, databaseFieldProtector));

            ConfigureProperty<CottonServerSettings>(
                modelBuilder,
                settings => settings.CloudServicesTokenEncrypted,
                converter);
            ConfigureProperty<CottonServerSettings>(
                modelBuilder,
                settings => settings.OidcClientSecretEncrypted,
                converter);
            ConfigureProperty<CottonServerSettings>(
                modelBuilder,
                settings => settings.S3SecretAccessKeyEncrypted,
                converter);
            ConfigureProperty<CottonServerSettings>(
                modelBuilder,
                settings => settings.SmtpPasswordEncrypted,
                converter);
            ConfigureProperty<OidcProvider>(
                modelBuilder,
                provider => provider.ClientSecretEncrypted,
                converter);
            ConfigureProperty<OidcLoginState>(
                modelBuilder,
                state => state.CodeVerifierEncrypted,
                converter);
            ConfigureProperty<OidcLoginState>(
                modelBuilder,
                state => state.NonceEncrypted,
                converter);
        }

        private static void ConfigureProperty<TEntity>(
            ModelBuilder modelBuilder,
            Expression<Func<TEntity, string?>> property,
            ValueConverter<string?, string?> converter)
            where TEntity : class
        {
            modelBuilder.Entity<TEntity>()
                .Property(property)
                .HasConversion(converter);
        }

        private static string? Protect(
            string? value,
            IDatabaseFieldProtector? databaseFieldProtector)
        {
            if (value is null)
            {
                return null;
            }

            if (databaseFieldProtector is null)
            {
                throw CreateMissingDatabaseFieldProtectorException();
            }

            return databaseFieldProtector.Protect(value);
        }

        private static string? Unprotect(
            string? value,
            IDatabaseFieldProtector? databaseFieldProtector)
        {
            if (value is null)
            {
                return null;
            }

            if (databaseFieldProtector is null)
            {
                throw CreateMissingDatabaseFieldProtectorException();
            }

            return databaseFieldProtector.Unprotect(value);
        }

        private static InvalidOperationException CreateMissingDatabaseFieldProtectorException()
        {
            return new InvalidOperationException(
                "Encrypted EF string conversion requires IDatabaseFieldProtector. Construct CottonDbContext with a field protector before accessing encrypted properties.");
        }
    }
}
