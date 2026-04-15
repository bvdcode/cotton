using Cotton.Database;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Helpers
{
    public static class UsernameHelpers
    {
        public static async Task<string> BuildAvailableUsernameFromEmailAsync(
            CottonDbContext dbContext,
            string email,
            CancellationToken cancellationToken = default)
        {
            string localPart = email.Split('@', 2)[0].Trim().ToLowerInvariant();
            var raw = localPart.Where(static c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')).ToArray();
            string candidate = raw.Length == 0 ? "user" : new string(raw);

            if (candidate[0] >= '0' && candidate[0] <= '9')
            {
                candidate = $"u{candidate}";
            }

            if (candidate.Length < UsernameValidator.MinLength)
            {
                candidate = candidate.PadRight(UsernameValidator.MinLength, '0');
            }

            if (candidate.Length > UsernameValidator.MaxLength)
            {
                candidate = candidate[..UsernameValidator.MaxLength];
            }

            if (!UsernameValidator.TryNormalizeAndValidate(candidate, out var normalized, out _))
            {
                normalized = "user";
            }

            if (!await dbContext.Users.AnyAsync(x => x.Username == normalized, cancellationToken))
            {
                return normalized;
            }

            for (int i = 0; i < 10; i++)
            {
                string suffix = Guid.NewGuid().ToString("N")[..6];
                int maxBaseLength = UsernameValidator.MaxLength - suffix.Length;
                string basePart = normalized[..Math.Min(normalized.Length, maxBaseLength)];
                string withSuffix = $"{basePart}{suffix}";
                if (await dbContext.Users.AnyAsync(x => x.Username == withSuffix, cancellationToken))
                {
                    continue;
                }

                return withSuffix;
            }

            return $"user{Guid.NewGuid():N}"[..UsernameValidator.MaxLength];
        }
    }
}
