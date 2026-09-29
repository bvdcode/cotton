// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Enums;
using Cotton.Server.Models.Requests;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using System.Globalization;

namespace Cotton.Server.Services
{
    public static class DirectoryPageFilter
    {
        public static IQueryable<DirectoryPageEntry> Apply(
            IQueryable<DirectoryPageEntry> query, DirectoryListingOptions options)
        {
            if (options.FilterOperator == DirectoryFilterOperator.None)
            {
                return query;
            }
            return options.FilterBy switch
            {
                DirectoryField.Name => FilterNames(query, options),
                DirectoryField.SizeBytes => FilterSizes(query, options),
                _ => throw new BadRequestException("Invalid directory filter field."),
            };
        }

        private static IQueryable<DirectoryPageEntry> FilterNames(
            IQueryable<DirectoryPageEntry> query, DirectoryListingOptions options)
        {
            string value = NameValidator.GetNameKey(options.FilterValue?.Trim() ?? string.Empty);
            string[] values = options.FilterValues.Select(value => NameValidator.GetNameKey(value.Trim())).ToArray();
            return options.FilterOperator switch
            {
                DirectoryFilterOperator.Contains => query.Where(item => item.NameKey.Contains(value)),
                DirectoryFilterOperator.DoesNotContain => query.Where(item => !item.NameKey.Contains(value)),
                DirectoryFilterOperator.Equals => query.Where(item => item.NameKey == value),
                DirectoryFilterOperator.DoesNotEqual => query.Where(item => item.NameKey != value),
                DirectoryFilterOperator.StartsWith => query.Where(item => item.NameKey.StartsWith(value)),
                DirectoryFilterOperator.EndsWith => query.Where(item => item.NameKey.EndsWith(value)),
                DirectoryFilterOperator.IsEmpty => query.Where(item => item.NameKey == string.Empty),
                DirectoryFilterOperator.IsNotEmpty => query.Where(item => item.NameKey != string.Empty),
                DirectoryFilterOperator.IsAnyOf => query.Where(item => values.Contains(item.NameKey)),
                _ => throw new BadRequestException("Invalid name filter operator."),
            };
        }

        private static IQueryable<DirectoryPageEntry> FilterSizes(
            IQueryable<DirectoryPageEntry> query, DirectoryListingOptions options)
        {
            if (options.FilterOperator == DirectoryFilterOperator.IsEmpty)
            {
                return query.Where(item => item.SizeBytes == null);
            }
            if (options.FilterOperator == DirectoryFilterOperator.IsNotEmpty)
            {
                return query.Where(item => item.SizeBytes != null);
            }
            if (options.FilterOperator == DirectoryFilterOperator.IsAnyOf)
            {
                double[] values = options.FilterValues.Select(ParseSize).ToArray();
                return query.Where(item => item.SizeBytes != null && values.Contains((double)item.SizeBytes));
            }
            double value = ParseSize(options.FilterValue ?? string.Empty);
            return options.FilterOperator switch
            {
                DirectoryFilterOperator.Equals => query.Where(item => item.SizeBytes != null && item.SizeBytes == value),
                DirectoryFilterOperator.DoesNotEqual => query.Where(item => item.SizeBytes != null && item.SizeBytes != value),
                DirectoryFilterOperator.GreaterThan => query.Where(item => item.SizeBytes > value),
                DirectoryFilterOperator.GreaterThanOrEqual => query.Where(item => item.SizeBytes >= value),
                DirectoryFilterOperator.LessThan => query.Where(item => item.SizeBytes < value),
                DirectoryFilterOperator.LessThanOrEqual => query.Where(item => item.SizeBytes <= value),
                _ => throw new BadRequestException("Invalid size filter operator."),
            };
        }

        private static double ParseSize(string input)
        {
            if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || !double.IsFinite(value))
            {
                throw new BadRequestException("Invalid size filter value.");
            }
            return value;
        }
    }
}
