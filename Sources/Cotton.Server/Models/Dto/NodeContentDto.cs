using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class NodeContentDto : BaseDto<Guid>
    {
        public ICollection<UserLayoutNodeDto> Nodes { get; set; } = [];
        public ICollection<FileManifestDto> Files { get; set; } = [];
    }
}
