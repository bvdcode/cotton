// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models
{
    public class TotpSetup
    {
        public string SecretBase32 { get; set; } = null!;
        public string OtpAuthUri { get; set; } = null!;
    }
}
