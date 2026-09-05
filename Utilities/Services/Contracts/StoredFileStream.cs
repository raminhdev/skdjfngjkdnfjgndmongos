namespace M1Mentor.Utilities.Services.Contracts
{
    /// <summary>
    /// A readable, seekable handle to a stored file plus its known length. Always dispose
    /// (or <c>await using</c>) the returned instance to release the underlying stream / GridFS
    /// download handle. The stream is suitable for ASP.NET range-processing downloads.
    /// </summary>
    public sealed class StoredFileStream : IAsyncDisposable, IDisposable
    {
        public required Stream Stream { get; init; }
        public required long Length { get; init; }

        public void Dispose() => Stream?.Dispose();

        public async ValueTask DisposeAsync()
        {
            if (Stream != null)
                await Stream.DisposeAsync();
        }
    }
}
