using Cotton.Database;
using Cotton.Server.Models.Dto;
using EasyExtensions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Hubs
{
    [Authorize]
    [EnableCors]
    public class EventHub(CottonDbContext _dbContext) : Hub
    {
        public const string NotificationMethod = "OnNotificationReceived";

        public override async Task OnConnectedAsync()
        {
            Guid userId = Context.User.GetUserId();
            var unread = await _dbContext.Notifications
                .Where(x => x.UserId == userId && !x.ReadAt.HasValue)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            if (unread.Count > 0)
            {
                var latest = unread.First();
                var dto = latest.Adapt<NotificationDto>();
                await Clients.Caller.SendAsync(NotificationMethod, dto);
            }
            await base.OnConnectedAsync();
        }
    }
}
