using Mapster;
using Cotton.Server.Models;
using Cotton.Server.Database;
using Cotton.Server.Models.Dto;
using Microsoft.AspNetCore.Mvc;
using Cotton.Server.Models.Requests;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Models;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileController(CottonDbContext _dbContext) : ControllerBase
    {
        [HttpGet(Routes.Files)]
        public async Task<CottonResult> GetFiles()
        {
            var all = await _dbContext.FileManifests.ToListAsync();
            var mapped = all.Adapt<FileManifestDto[]>();
            return CottonResult.Ok("Files retrieved successfully.", mapped);
        }

        [HttpGet(Routes.Files + "/{fileId}/download")]
        public async Task<CottonResult> DownloadFile([FromRoute] Guid fileId)
        {
            return CottonResult.Ok("");
        }

        [HttpPost(Routes.Files)]
        public async Task<CottonResult> CreateFileFromChunks([FromBody] CreateFileRequest request)
        {
            FileManifest newFile = new()
            {
                ContentType = request.ContentType,
                Folder = request.Folder,
                Name = request.Name,
                Sha256 = Convert.FromHexString(request.Sha256),
                SizeBytes = 0
            };
            await _dbContext.FileManifests.AddAsync(newFile);
            await _dbContext.SaveChangesAsync();




            return CottonResult.Ok("");
        }
    }
}
