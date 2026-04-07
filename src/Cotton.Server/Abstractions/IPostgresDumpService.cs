namespace Cotton.Server.Abstractions
{
    public interface IPostgresDumpService
    {
        Task DumpToFileAsync(string outputFilePath, CancellationToken cancellationToken = default);
    }
}
