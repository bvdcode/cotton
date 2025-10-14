using Cotton.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Cotton.Server.Models.Requests;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileController : ControllerBase
    {
        [HttpGet(Routes.Files + "/{fileId}/download")]
        public async Task<CottonResult> DownloadFile([FromRoute] Guid fileId)
        {
            return CottonResult.Ok("");
        }

        [HttpPost(Routes.Files)]
        public async Task<CottonResult> CreateFileFromChunks([FromBody] CreateFileRequest request)
        {
            return CottonResult.Ok("");
        }
    }
}
