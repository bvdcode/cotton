namespace Cotton.Localization
{
    public static class EmailTemplates
    {
        public static string EmailVerificationSubject => "Verify your email";

        public static string EmailVerificationBodyHtml(
            string username,
            string verifyUrl)
        {
            return $"""
            <!doctype html>
            <html lang=\"en\">
              <head>
                <meta charset=\"utf-8\" />
                <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />
                <title>Verify your email</title>
              </head>
              <body style=\"font-family:system-ui,-apple-system,Segoe UI,Roboto,Ubuntu,Cantarell,Noto Sans,sans-serif;line-height:1.5\">
                <h2 style=\"margin:0 0 12px 0\">Verify your email</h2>
                <p style=\"margin:0 0 12px 0\">Hi {System.Net.WebUtility.HtmlEncode(username)},</p>
                <p style=\"margin:0 0 12px 0\">Please confirm your email address by clicking the button below:</p>
                <p style=\"margin:0 0 16px 0\">
                  <a href=\"{System.Net.WebUtility.HtmlEncode(verifyUrl)}\"
                     style=\"display:inline-block;padding:10px 14px;background:#2563eb;color:#fff;text-decoration:none;border-radius:8px\">
                    Verify email
                  </a>
                </p>
                <p style=\"margin:0 0 12px 0\">Or copy and open this link:</p>
                <p style=\"margin:0 0 16px 0\">
                  <a href=\"{System.Net.WebUtility.HtmlEncode(verifyUrl)}\">{System.Net.WebUtility.HtmlEncode(verifyUrl)}</a>
                </p>
                <p style=\"margin:0\">If you didn't request this, you can ignore this email.</p>
              </body>
            </html>
            """;
        }
    }
}
