using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class UserDto : BaseDto<Guid>
    {
        public string Username { get; set; } = null!;
    }
}
