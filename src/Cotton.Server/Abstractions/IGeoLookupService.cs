using EasyExtensions.Clients.Models;
using System.Net;

namespace Cotton.Server.Abstractions
{
    public interface IGeoLookupService
    {
        Task<GeoIpInfo?> TryLookupAsync(IPAddress ipAddress, CancellationToken cancellationToken = default);
    }
}
