namespace Cotton.Previews.Streams
{
    internal sealed class LeaveOpenFileStream(string path, FileStreamOptions options) : FileStream(path, options)
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
