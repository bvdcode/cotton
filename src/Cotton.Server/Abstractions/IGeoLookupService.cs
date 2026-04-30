using Cotton.Server.Models;
using System.Net;

namespace Cotton.Server.Abstractions
{
    public interface IGeoLookupService
    {
        Task<GeoLookupResult?> TryLookupAsync(IPAddress ipAddress, CancellationToken cancellationToken = default);
    }
}
