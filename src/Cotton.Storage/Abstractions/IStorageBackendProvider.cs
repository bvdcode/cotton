namespace Cotton.Storage.Abstractions
{
    public interface IStorageBackendProvider
    {
        IStorageBackend GetBackend();
    }
}
