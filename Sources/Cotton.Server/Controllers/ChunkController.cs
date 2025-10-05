using Cotton.Server.Database;
using Cotton.Server.Settings;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    public class ChunkController(CottonDbContext _dbContext, CottonSettings _settings) : ControllerBase
    {
        [HttpPost(Routes.Chunks)]
        public async Task<CottonResult> UploadChunk(IFormFile file, string hash)
        {
            if (file == null || file.Length == 0)
            {
                return CottonResult.BadRequest("No file uploaded.");
            }
            if (file.Length > _settings.ChunkSizeBytes)
            {
                return CottonResult.BadRequest($"File size exceeds maximum chunk size of {_settings.ChunkSizeBytes} bytes.");
            }
            if (string.IsNullOrWhiteSpace(hash) || hash.Length != 64 || !System.Text.RegularExpressions.Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$"))
            {
                return CottonResult.BadRequest("Invalid SHA-256 hash format.");
            }

            return CottonResult.Success("", "");
        }
    }

    public class CottonResult : IActionResult
    {
        public bool Result { get; set; }
        public string Message { get; set; } = string.Empty;
        public object Data { get; set; } = null!;
        public static CottonResult Success(string message, object data)
        {
            return new CottonResult { Result = true, Message = message, Data = data };
        }
        public static CottonResult BadRequest(string message)
        {
            return new CottonResult { Result = false, Message = message };
        }

        public Task ExecuteResultAsync(ActionContext context)
        {
            throw new NotImplementedException();
        }
    }
}
