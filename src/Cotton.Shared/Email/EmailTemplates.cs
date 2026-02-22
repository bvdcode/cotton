namespace Cotton.Email
{
    /// <summary>
    /// Contains all email HTML templates as compile-time constants.
    /// Single source of truth for both self-hosted (SMTP) and cloud (Gateway) modes.
    /// Placeholders use <c>{{variable_name}}</c> syntax.
    /// </summary>
    public static class EmailTemplates
    {
        private const string CommonHeader =
            @"          <!-- Header -->
          <tr>
            <td style=""background:linear-gradient(135deg,#1bcea7,#96be02);padding:36px 40px;text-align:center;"">
              <img src=""cid:cotton-logo"" alt=""Cotton Cloud"" width=""48"" height=""48"" style=""display:block;margin:0 auto 12px;"" />
              <h1 style=""margin:0;color:#ffffff;font-size:24px;font-weight:700;letter-spacing:-0.5px;"">Cotton Cloud</h1>
            </td>
          </tr>
";

        private const string CommonFooterEn =
            @"          <!-- Footer -->
          <tr>
            <td style=""padding:24px 40px;border-top:1px solid #eaeaec;text-align:center;"">
              <p style=""margin:0;color:#b0b0b8;font-size:12px;"">&copy; <a href=""https://github.com/bvdcode/cotton"" target=""_blank"" style=""color:#b0b0b8;text-decoration:underline;"">{{year}} Cotton Cloud </a> &middot; &middot; All rights reserved</p>
            </td>
          </tr>
";

        private const string CommonFooterRu =
            @"          <!-- Footer -->
          <tr>
            <td style=""padding:24px 40px;border-top:1px solid #eaeaec;text-align:center;"">
              <p style=""margin:0;color:#b0b0b8;font-size:12px;"">&copy; <a href=""https://github.com/bvdcode/cotton"" target=""_blank"" style=""color:#b0b0b8;text-decoration:underline;""> {{year}} Cotton Cloud </a> &middot; &middot; Все права защищены</p>
            </td>
          </tr>
";

        private const string CommonShellClose =
            @"        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

        private const string ShellOpenEmailConfirmationEn =
            @"<!DOCTYPE html
<html lang=""en"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>Confirm your email</title>
</head>
<body style=""margin:0;padding:0;background-color:#f4f4f7;font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f4f4f7;padding:40px 0;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""560"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);"">
";

        private const string BodyEmailConfirmationEn =
            @"          <!-- Body -->
          <tr>
            <td style=""padding:40px 40px 32px;"">
              <h2 style=""margin:0 0 16px;color:#1a1a2e;font-size:20px;font-weight:600;"">Hello, {{recipient_name}}!</h2>
              <p style=""margin:0 0 24px;color:#51545e;font-size:15px;line-height:1.6;"">
                Thank you for registering. Please confirm your email address by clicking the button below.
              </p>
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:0 auto 24px;"">
                <tr>
                  <td align=""center"" style=""border-radius:8px;background:linear-gradient(135deg,#96be02,#1bcea7);"">
                    <a href=""{{confirmation_url}}"" target=""_blank"" style=""display:inline-block;padding:14px 32px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:8px;"">
                      Confirm Email
                    </a>
                  </td>
                </tr>
              </table>
              <p style=""margin:0 0 8px;color:#51545e;font-size:13px;line-height:1.5;"">
                Or copy this link into your browser:
              </p>
              <p style=""margin:0 0 24px;word-break:break-all;color:#1bcea7;font-size:13px;line-height:1.5;"">
                {{confirmation_url}}
              </p>
              <p style=""margin:0;color:#9ca3af;font-size:13px;line-height:1.5;"">
                If you didn't create an account, just ignore this message.
              </p>
            </td>
          </tr>
";

        private const string ShellOpenEmailConfirmationRu =
            @"<!DOCTYPE html
<html lang=""ru"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>Подтвердите вашу почту</title>
  <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"" />
</head>
<body style=""margin:0;padding:0;background-color:#f4f4f7;font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f4f4f7;padding:40px 0;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""560"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);"">
";

        private const string BodyEmailConfirmationRu =
            @"          <!-- Body -->
          <tr>
            <td style=""padding:40px 40px 32px;"">
              <h2 style=""margin:0 0 16px;color:#1a1a2e;font-size:20px;font-weight:600;"">Здравствуйте, {{recipient_name}}!</h2>
              <p style=""margin:0 0 24px;color:#51545e;font-size:15px;line-height:1.6;"">
                Спасибо за регистрацию. Пожалуйста, подтвердите ваш адрес электронной почты, нажав на кнопку ниже.
              </p>
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:0 auto 24px;"">
                <tr>
                  <td align=""center"" style=""border-radius:8px;background:linear-gradient(135deg,#96be02,#1bcea7);"">
                    <a href=""{{confirmation_url}}"" target=""_blank"" style=""display:inline-block;padding:14px 32px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:8px;"">
                      Подтвердить email
                    </a>
                  </td>
                </tr>
              </table>
              <p style=""margin:0 0 8px;color:#51545e;font-size:13px;line-height:1.5;"">
                Или скопируйте эту ссылку в браузер:
              </p>
              <p style=""margin:0 0 24px;word-break:break-all;color:#1bcea7;font-size:13px;line-height:1.5;"">
                {{confirmation_url}}
              </p>
              <p style=""margin:0;color:#9ca3af;font-size:13px;line-height:1.5;"">
                Если вы не запрашивали создание аккаунта, просто проигнорируйте это сообщение.
              </p>
            </td>
          </tr>
";

        private const string ShellOpenPasswordResetEn =
            @"<!DOCTYPE html
<html lang=""en"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>Reset your password</title>
</head>
<body style=""margin:0;padding:0;background-color:#f4f4f7;font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f4f4f7;padding:40px 0;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""560"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);"">
";

        private const string BodyPasswordResetEn =
            @"          <!-- Body -->
          <tr>
            <td style=""padding:40px 40px 32px;"">
              <h2 style=""margin:0 0 16px;color:#1a1a2e;font-size:20px;font-weight:600;"">Hello, {{recipient_name}}!</h2>
              <p style=""margin:0 0 24px;color:#51545e;font-size:15px;line-height:1.6;"">
                We received a request to reset your password. Click the button below to choose a new one.
              </p>
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:0 auto 24px;"">
                <tr>
                  <td align=""center"" style=""border-radius:8px;background:linear-gradient(135deg,#96be02,#1bcea7);"">
                    <a href=""{{reset_url}}"" target=""_blank"" style=""display:inline-block;padding:14px 32px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:8px;"">
                      Reset Password
                    </a>
                  </td>
                </tr>
              </table>
              <p style=""margin:0 0 8px;color:#51545e;font-size:13px;line-height:1.5;"">
                Or copy this link into your browser:
              </p>
              <p style=""margin:0 0 24px;word-break:break-all;color:#1bcea7;font-size:13px;line-height:1.5;"">
                {{reset_url}}
              </p>
              <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:0 0 24px;"">
                <tr>
                  <td style=""padding:16px 20px;background-color:#fef3c7;border-radius:8px;border-left:4px solid #f59e0b;"">
                    <p style=""margin:0;color:#92400e;font-size:13px;line-height:1.5;font-weight:500;"">
                      &#9888;&#65039; This link will expire soon. If you didn't request a password reset, please ignore this email &mdash; your password will remain unchanged.
                    </p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
";

        private const string ShellOpenPasswordResetRu =
            @"<!DOCTYPE html
<html lang=""ru"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>Сброс пароля</title>
  <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"" />
</head>
<body style=""margin:0;padding:0;background-color:#f4f4f7;font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f4f4f7;padding:40px 0;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""560"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);"">
";

        private const string BodyPasswordResetRu =
            @"          <!-- Body -->
          <tr>
            <td style=""padding:40px 40px 32px;"">
              <h2 style=""margin:0 0 16px;color:#1a1a2e;font-size:20px;font-weight:600;"">Здравствуйте, {{recipient_name}}!</h2>
              <p style=""margin:0 0 24px;color:#51545e;font-size:15px;line-height:1.6;"">
                Мы получили запрос на сброс пароля для вашего аккаунта. Нажмите на кнопку ниже, чтобы задать новый пароль.
              </p>
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:0 auto 24px;"">
                <tr>
                  <td align=""center"" style=""border-radius:8px;background:linear-gradient(135deg,#96be02,#1bcea7);"">
                    <a href=""{{reset_url}}"" target=""_blank"" style=""display:inline-block;padding:14px 32px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:8px;"">
                      Сбросить пароль
                    </a>
                  </td>
                </tr>
              </table>
              <p style=""margin:0 0 8px;color:#51545e;font-size:13px;line-height:1.5;"">
                Или скопируйте эту ссылку в браузер:
              </p>
              <p style=""margin:0 0 24px;word-break:break-all;color:#1bcea7;font-size:13px;line-height:1.5;"">
                {{reset_url}}
              </p>
              <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:0 0 24px;"">
                <tr>
                  <td style=""padding:16px 20px;background-color:#fef3c7;border-radius:8px;border-left:4px solid #f59e0b;"">
                    <p style=""margin:0;color:#92400e;font-size:13px;line-height:1.5;font-weight:500;"">
                      &#9888;&#65039; Срок действия ссылки ограничен. Если вы не запрашивали сброс пароля, просто проигнорируйте это письмо &mdash; ваш пароль останется прежним.
                    </p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
";

        /// <summary>
        /// Represents the HTML template for the email confirmation message sent to users upon registration.
        /// </summary>
        /// <remarks>This template includes placeholders for the recipient's name and confirmation URL,
        /// which should be replaced with actual values when sending the email. The design is responsive and styled for
        /// a clean presentation across devices.</remarks>
        public const string EmailConfirmationEn =
            ShellOpenEmailConfirmationEn
            + CommonHeader
            + BodyEmailConfirmationEn
            + CommonFooterEn
            + CommonShellClose;

        /// <summary>
        /// Represents the HTML template for the Russian-language email confirmation message sent to users after
        /// registration.
        /// </summary>
        /// <remarks>This template includes placeholders for the recipient's name and confirmation URL,
        /// which must be replaced with actual values before sending. The message provides instructions for confirming
        /// the email address and includes a fallback link for manual copying. Intended for use in automated email
        /// workflows.</remarks>
        public const string EmailConfirmationRu =
            ShellOpenEmailConfirmationRu
            + CommonHeader
            + BodyEmailConfirmationRu
            + CommonFooterRu
            + CommonShellClose;

        /// <summary>
        /// Contains the HTML template for the password reset email in English.
        /// </summary>
        public const string PasswordResetEn =
            ShellOpenPasswordResetEn
            + CommonHeader
            + BodyPasswordResetEn
            + CommonFooterEn
            + CommonShellClose;

        /// <summary>
        /// Contains the HTML template for the password reset email in Russian.
        /// </summary>
        /// <remarks>This constant string is used to generate a password reset email, which includes a
        /// link for the user to reset their password. The placeholders {{recipient_name}} and {{reset_url}} should be
        /// replaced with the actual recipient's name and the password reset link, respectively.</remarks>
        public const string PasswordResetRu =
            ShellOpenPasswordResetRu
            + CommonHeader
            + BodyPasswordResetRu
            + CommonFooterRu
            + CommonShellClose;
    }
}
