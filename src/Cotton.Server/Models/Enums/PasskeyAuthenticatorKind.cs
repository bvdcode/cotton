// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text.Json.Serialization;

namespace Cotton.Server.Models.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter<PasskeyAuthenticatorKind>))]
    public enum PasskeyAuthenticatorKind
    {
        Unknown = 0,

        SecurityKey = 1,

        Device = 2
    }
}
