namespace M1Mentor.Utilities.Services.Contracts
{
    /// <summary>
    /// Abstraction over the physical file store. Implementations must stream both writes and
    /// reads — they must NEVER buffer a whole file into memory or into a Mongo document — so the
    /// system can handle multi-GB uploads/downloads with bounded memory. Storage is swappable via
    /// configuration (Local, GridFS, future S3); callers depend only on this interface.
    /// </summary>
    public interface IFileStorageProvider
    {
        /// <summary>Stable provider key persisted on the file metadata (e.g. "Local", "GridFs", "S3", "MinIO").</summary>
        string Name { get; }

        /// <summary>
        /// The bucket/container the file lives in for object-store backends (S3, MinIO), persisted on
        /// the metadata. Null for path-based backends (Local) or document-store backends (GridFs).
        /// </summary>
        string BucketName { get; }

        /// <summary>
        /// Streams <paramref name="source"/> into the store under <paramref name="relativePath"/>.
        /// Returns the number of bytes written. Implementations create any required directories
        /// and overwrite an existing entry at the same path.
        /// </summary>
        Task<long> SaveAsync(Stream source, string relativePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a seekable read stream for the stored file, or throws if it does not exist.
        /// </summary>
        Task<StoredFileStream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

        /// <summary>Deletes the stored file. Returns false if it was already absent.</summary>
        Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

        /// <summary>Cheap existence check without opening the payload.</summary>
        Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default);
    }
}
