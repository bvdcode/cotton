namespace Cotton.Server.Abstractions
{
    public interface IDatabaseAutoRestoreService
    {
        Task TryRestoreIfEmptyAsync(CancellationToken cancellationToken = default);
    }
}
