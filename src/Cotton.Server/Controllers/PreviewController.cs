using Cotton.Storage.Abstractions;
using EasyExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class PreviewController(IStoragePipeline _storage) : ControllerBase
    {
        [Authorize]
        [HttpGet("/api/v1/preview/{previewId:guid}")]
        public async Task<IActionResult> GetFilePreview([FromRoute] Guid previewId)
        {
            _ = previewId;
            _ = _storage;
            //Guid userId = User.GetUserId();
            //var nodeFile = await _dbContext.NodeFiles
            //    .FirstOrDefaultAsync(x => x.Id == nodeFileId && x.OwnerId == userId);
            //if (nodeFile == null)
            //{
            //    return CottonResult.NotFound("Node file not found");
            //}
            return Ok();
        }
    }
}
