using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class UserLayoutNodeDto : BaseDto<Guid>
    {
        public Guid LayoutId { get; set; }
        public Guid? ParentNodeId { get; set; }
        public string Name { get; set; } = null!;
    }
}
