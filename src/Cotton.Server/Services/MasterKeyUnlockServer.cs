using Cotton.Autoconfig.Extensions;
using System.Security.Cryptography;
using System.Text.Json;

namespace Cotton.Server.Services
{
    public static class MasterKeyUnlockServer
    {
        private const string UnlockPageHtml = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Cotton unlock</title>
  <style>
    :root {
      color-scheme: light dark;
      font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
      background: #f6f7f9;
      color: #111827;
    }
    @media (prefers-color-scheme: dark) {
      :root { background: #111318; color: #f3f4f6; }
    }
    * { box-sizing: border-box; }
    body {
      min-height: 100vh;
      margin: 0;
      display: grid;
      place-items: center;
      padding: 24px;
    }
    main {
      width: min(100%, 440px);
      border: 1px solid color-mix(in srgb, currentColor 12%, transparent);
      border-radius: 8px;
      padding: 28px;
      background: color-mix(in srgb, canvas 94%, currentColor 6%);
      box-shadow: 0 24px 60px rgb(15 23 42 / 12%);
    }
    h1 {
      margin: 0 0 8px;
      font-size: 24px;
      line-height: 1.2;
      letter-spacing: 0;
    }
    p {
      margin: 0 0 22px;
      color: color-mix(in srgb, currentColor 72%, transparent);
      line-height: 1.5;
    }
    label {
      display: block;
      margin-bottom: 8px;
      font-size: 14px;
      font-weight: 650;
    }
    input {
      width: 100%;
      height: 44px;
      border: 1px solid color-mix(in srgb, currentColor 18%, transparent);
      border-radius: 6px;
      padding: 0 12px;
      font: inherit;
      background: canvas;
      color: inherit;
    }
    .actions {
      display: flex;
      gap: 10px;
      margin-top: 16px;
    }
    button {
      min-height: 40px;
      border: 1px solid transparent;
      border-radius: 6px;
      padding: 0 14px;
      font: inherit;
      font-weight: 650;
      cursor: pointer;
    }
    button[type="submit"] {
      flex: 1;
      background: #2563eb;
      color: white;
    }
    button[type="button"] {
      background: transparent;
      color: inherit;
      border-color: color-mix(in srgb, currentColor 18%, transparent);
    }
    button:disabled {
      opacity: .65;
      cursor: wait;
    }
    #status {
      min-height: 22px;
      margin-top: 16px;
      font-size: 14px;
      color: color-mix(in srgb, currentColor 76%, transparent);
    }
    #status.error { color: #dc2626; }
    #status.ok { color: #16a34a; }
  </style>
</head>
<body>
  <main>
    <h1>Unlock Cotton</h1>
    <p>The main application is stopped until a valid master key is provided.</p>
    <form id="unlock-form" autocomplete="off">
      <label for="masterKey">Master key</label>
      <input id="masterKey" name="masterKey" type="password" minlength="32" maxlength="32" required spellcheck="false" autocomplete="off">
      <div class="actions">
        <button type="submit">Unlock</button>
        <button type="button" id="generate">Generate</button>
      </div>
      <div id="status" role="status" aria-live="polite"></div>
    </form>
  </main>
  <script>
    const form = document.getElementById("unlock-form");
    const input = document.getElementById("masterKey");
    const status = document.getElementById("status");
    const generate = document.getElementById("generate");

    function setStatus(message, kind) {
      status.textContent = message;
      status.className = kind || "";
    }

    generate.addEventListener("click", async () => {
      generate.disabled = true;
      try {
        const response = await fetch("/unlock/key", { cache: "no-store" });
        if (!response.ok) throw new Error("Failed to generate key.");
        const key = (await response.text()).trim();
        input.value = key;
        input.type = "text";
        input.focus();
        input.select();
        setStatus("Generated. Store this key before unlocking.", "ok");
      } catch (error) {
        setStatus(error.message || "Failed to generate key.", "error");
      } finally {
        generate.disabled = false;
      }
    });

    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const masterKey = input.value.trim();
      const submit = form.querySelector('button[type="submit"]');
      submit.disabled = true;
      generate.disabled = true;
      setStatus("Checking key...", "");
      try {
        const response = await fetch("/unlock", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ masterKey })
        });
        const data = await response.json();
        if (!response.ok || !data.ok) throw new Error(data.message || "Unlock failed.");
        input.value = "";
        setStatus(data.message || "Unlocked. Cotton is starting.", "ok");
        setTimeout(() => window.location.replace("/"), 2400);
      } catch (error) {
        setStatus(error.message || "Unlock failed.", "error");
        submit.disabled = false;
        generate.disabled = false;
      }
    });
  </script>
</body>
</html>
""";

        public static async Task<CottonEncryptionSettings> WaitForUnlockAsync(string[] args)
        {
            var completion = new TaskCompletionSource<CottonEncryptionSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.AddFilter(
                "Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager",
                LogLevel.Error);

            var app = builder.Build();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Cotton.Server.Unlock");
            var sentinel = new MasterKeySentinelStore(
                app.Services.GetRequiredService<ILogger<MasterKeySentinelStore>>());
            var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

            app.Use(async (context, next) =>
            {
                if (IsUnlockEndpoint(context.Request))
                {
                    await next();
                    return;
                }

                RedirectToUnlock(context);
            });

            app.MapGet("/unlock", () => Results.Content(UnlockPageHtml, "text/html; charset=utf-8"));
            app.MapGet("/unlock/key", () => Results.Text(GenerateRootMasterKey(), "text/plain; charset=utf-8"));
            app.MapPost("/unlock", async (HttpContext context) =>
            {
                string? rootMasterKey = await ReadSubmittedMasterKeyAsync(context);
                CottonEncryptionSettings encryptionSettings;
                try
                {
                    encryptionSettings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(rootMasterKey ?? string.Empty);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new UnlockResponse(false, ex.Message));
                }

                MasterKeySentinelResult validation = await sentinel.ValidateOrInitializeAsync(
                    encryptionSettings,
                    context.RequestAborted);
                if (!validation.Success)
                {
                    return Results.BadRequest(new UnlockResponse(false, validation.Error ?? "Unlock failed."));
                }

                _ = CompleteUnlockAsync(completion, app, encryptionSettings);
                string message = validation.Created
                    ? "Master key initialized. Cotton is starting."
                    : "Master key accepted. Cotton is starting.";
                return Results.Ok(new UnlockResponse(true, message));
            });

            using var stoppingRegistration = lifetime.ApplicationStopping.Register(
                () => completion.TrySetCanceled());

            await app.StartAsync();
            LogUnlockAddresses(app, logger);

            try
            {
                return await completion.Task;
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }

        private static bool IsUnlockEndpoint(HttpRequest request)
        {
            PathString path = request.Path;
            return path.Equals("/unlock", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/unlock/key", StringComparison.OrdinalIgnoreCase);
        }

        private static void RedirectToUnlock(HttpContext context)
        {
            context.Response.Headers.Location = "/unlock";
            context.Response.StatusCode = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
                ? StatusCodes.Status302Found
                : StatusCodes.Status303SeeOther;
        }

        private static async Task CompleteUnlockAsync(
            TaskCompletionSource<CottonEncryptionSettings> completion,
            IHost host,
            CottonEncryptionSettings encryptionSettings)
        {
            await Task.Delay(750);
            completion.TrySetResult(encryptionSettings);
            await host.StopAsync();
        }

        private static async Task<string?> ReadSubmittedMasterKeyAsync(HttpContext context)
        {
            if (context.Request.HasFormContentType)
            {
                IFormCollection form = await context.Request.ReadFormAsync();
                return form["masterKey"].ToString().Trim();
            }

            UnlockRequest? request = await JsonSerializer.DeserializeAsync<UnlockRequest>(
                context.Request.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                context.RequestAborted);
            return request?.MasterKey?.Trim();
        }

        private static string GenerateRootMasterKey() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

        private static void LogUnlockAddresses(WebApplication app, ILogger logger)
        {
            string[] addresses = [.. app.Urls];
            if (addresses.Length == 0)
            {
                logger.LogWarning("COTTON_MASTER_KEY is not configured. Open /unlock to provide the master key.");
                return;
            }

            foreach (string address in addresses)
            {
                logger.LogWarning(
                    "COTTON_MASTER_KEY is not configured. Unlock Cotton at {UnlockUrl}",
                    address.TrimEnd('/') + "/unlock");
            }
        }

        private sealed record UnlockRequest(string? MasterKey);
        private sealed record UnlockResponse(bool Ok, string Message);
    }
}
