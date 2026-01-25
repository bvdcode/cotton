using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;

namespace Cotton.Server.Hubs
{
    [Authorize]
    [EnableCors]
    public class EventHub : Hub
    {

    }
}
