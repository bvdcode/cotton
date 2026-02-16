namespace Cotton.Localization
{
    public static class EmailTemplates
    {
        public static string EmailVerificationSubject => "Verify your email";

        public static string EmailVerificationBodyHtml(
            string username,
            string verifyUrl)
        {
            return BuildHtmlEmail(
                "Verify your email",
                username,
                "Please confirm your email address by clicking the button below:",
                verifyUrl,
                "Verify email",
                "If you didn't request this, you can ignore this email.");
        }

        public static string PasswordResetSubject => "Reset your password";

        public static string PasswordResetBodyHtml(
            string username,
            string resetUrl)
        {
            return BuildHtmlEmail(
                "Reset your password",
                username,
                "We received a request to reset your password. Click the button below to choose a new one:",
                resetUrl,
                "Reset password",
                "If you didn't request a password reset, you can ignore this email. The link will expire shortly.");
        }

        private static string BuildHtmlEmail(
            string title,
            string username,
            string description,
            string actionUrl,
            string buttonText,
            string footer)
        {
            string safeUser = System.Net.WebUtility.HtmlEncode(username);
            string safeUrl = System.Net.WebUtility.HtmlEncode(actionUrl);
            string safeButton = System.Net.WebUtility.HtmlEncode(buttonText);

            return $"""
            <!doctype html>
            <html lang="en">
              <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>{title}</title>
              </head>
              <body style="font-family:system-ui,-apple-system,Segoe UI,Roboto,Ubuntu,Cantarell,Noto Sans,sans-serif;line-height:1.5">
                <h2 style="margin:0 0 12px 0">{title}</h2>
                <p style="margin:0 0 12px 0">Hi {safeUser},</p>
                <p style="margin:0 0 12px 0">{description}</p>
                <p style="margin:0 0 16px 0">
                  <a href="{safeUrl}"
                     style="display:inline-block;padding:10px 14px;background:#2563eb;color:#fff;text-decoration:none;border-radius:8px">
                    {safeButton}
                  </a>
                </p>
                <p style="margin:0 0 12px 0">Or copy and open this link:</p>
                <p style="margin:0 0 16px 0">
                  <a href="{safeUrl}">{safeUrl}</a>
                </p>
                <p style="margin:0">{footer}</p>
              </body>
            </html>
            """;
        }
    }
}
