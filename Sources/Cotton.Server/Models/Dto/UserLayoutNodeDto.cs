using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class UserLayoutNodeDto : BaseDto<Guid>
    {
        public Guid UserLayoutId { get; set; }
        public Guid? ParentId { get; set; }
        public string Name { get; set; } = null!;
    }
}
