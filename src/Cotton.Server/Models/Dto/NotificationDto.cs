using Cotton.Database.Models.Enums;
using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class NotificationDto : BaseDto<Guid>
    {
        public string Title { get; set; } = null!;
        public string? Content { get; set; }
        public DateTime? ReadAt { get; set; }
        public string? Metadata { get; set; }
        public Guid UserId { get; set; }
        public NotificationPriority Priority { get; set; }
    }
}
