using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    public class UploadController : ControllerBase
    {
        public async Task<CottonResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return CottonResult.Failure("No file uploaded.");
            }
            var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }
            var filePath = Path.Combine(uploadPath, file.FileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return CottonResult.Success("File uploaded successfully.", new { filePath });
        }
    }

    public class CottonResult
    {
        public bool Result { get; set; }
        public string Message { get; set; } = string.Empty;
        public object Data { get; set; } = null!;
        public static CottonResult Success(string message, object data)
        {
            return new CottonResult { Result = true, Message = message, Data = data };
        }
        public static CottonResult Failure(string message)
        {
            return new CottonResult { Result = false, Message = message };
        }
    }
}
