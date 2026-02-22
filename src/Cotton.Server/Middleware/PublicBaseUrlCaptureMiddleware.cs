using Cotton.Server.Providers;

namespace Cotton.Server.Middleware
{
    public sealed class PublicBaseUrlCaptureMiddleware(RequestDelegate _next)
    {
        public async Task InvokeAsync(HttpContext context, SettingsProvider settingsProvider)
        {
            // Best-effort: capture once; never block the request if it fails.
            try
            {
                await settingsProvider.EnsurePublicBaseUrlAsync(context.Request, context.RequestAborted);
            }
            catch
            {
                // Ignore: the server must still be able to serve requests.
            }

            await _next(context);
        }
    }
}
