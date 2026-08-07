// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Reflection;

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
                "Encrypted EF string conversion requires IDatabaseFieldProtector. Use a raw startup/probe DbContext for pre-unlock reads.");
        }
    }
}
