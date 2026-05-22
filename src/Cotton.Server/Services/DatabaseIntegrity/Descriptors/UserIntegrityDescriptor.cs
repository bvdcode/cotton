// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors;

public sealed class UserIntegrityDescriptor : DatabaseIntegrityDescriptor<User>
{
    public override string EntityName => "users";
    public override int SchemaVersion => 1;

    public override string GetEntityKey(User entity)
    {
        return entity.Id.ToString("D");
    }

    public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, User entity)
    {
        writer.WriteGuidField(nameof(entity.Id), entity.Id);
        writer.WriteStringField(nameof(entity.Username), entity.Username);
        writer.WriteStringField(nameof(entity.PasswordPhc), entity.PasswordPhc);
        writer.WriteStringField(nameof(entity.WebDavTokenPhc), entity.WebDavTokenPhc);
        writer.WriteStringField(nameof(entity.Email), entity.Email);
        writer.WriteBooleanField(nameof(entity.IsEmailVerified), entity.IsEmailVerified);
        writer.WriteStringField(nameof(entity.EmailVerificationToken), entity.EmailVerificationToken);
        writer.WriteNullableDateTimeField(nameof(entity.EmailVerificationTokenSentAt), entity.EmailVerificationTokenSentAt);
        writer.WriteStringField(nameof(entity.PasswordResetToken), entity.PasswordResetToken);
        writer.WriteNullableDateTimeField(nameof(entity.PasswordResetTokenSentAt), entity.PasswordResetTokenSentAt);
        writer.WriteInt32Field(nameof(entity.Role), (int)entity.Role);
        writer.WriteBooleanField(nameof(entity.IsTotpEnabled), entity.IsTotpEnabled);
        writer.WriteBytesField(nameof(entity.TotpSecretEncrypted), entity.TotpSecretEncrypted);
        writer.WriteNullableDateTimeField(nameof(entity.TotpEnabledAt), entity.TotpEnabledAt);
        writer.WriteInt32Field(nameof(entity.TotpFailedAttempts), entity.TotpFailedAttempts);
    }
}
