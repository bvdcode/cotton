// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Attributes
{
    /// <summary>Marks string properties that are encrypted through the EF value converter.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EncryptedAttribute : Attribute
    {
    }
}
