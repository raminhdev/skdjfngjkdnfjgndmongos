using M1Mentor.Utilities.Services.Contracts;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using Utilities.Exceptions.Common;
using Utilities.Models.Settings;
using Utilities.MongoDatabase.Contracts;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    /// <summary>
    /// GridFS-backed <see cref="IFileStorageProvider"/>. GridFS is MongoDB's native large-object
    /// store: it transparently splits a file into 255 KB chunks across a dedicated collection, so
    /// individual documents never approach the 16 MB BSON limit. Uploads and downloads are fully
    /// streamed. The <c>relativePath</c> is used as the GridFS filename (one logical file per path).
    /// </summary>
    public class GridFsStorageProvider : IFileStorageProvider, ISelfSingletonDependency
    {
        private readonly IGridFSBucket _bucket;
        private readonly FileStorageSettings _settings;

        public GridFsStorageProvider(IMonjoConnection connection, FileStorageSettings settings)
        {
            _settings = settings;
            _bucket = new GridFSBucket(connection.Database, new GridFSBucketOptions
            {
                BucketName = string.IsNullOrWhiteSpace(settings.GridFsBucketName) ? "files" : settings.GridFsBucketName
            });
        }

        public string Name => "GridFs";

        public string BucketName => null;

        public async Task<long> SaveAsync(Stream source, string relativePath, CancellationToken cancellationToken = default)
        {
            // Replace any previous file stored under the same logical name to keep upload idempotent.
            await DeleteAsync(relativePath, cancellationToken);

            await using var upload = await _bucket.OpenUploadStreamAsync(
                relativePath,
                new GridFSUploadOptions { ChunkSizeBytes = 255 * 1024 },
                cancellationToken);

            await source.CopyToAsync(upload, _settings.StreamBufferSize, cancellationToken);
            await upload.CloseAsync(cancellationToken);

            return upload.Length;
        }

        public async Task<StoredFileStream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var info = await FindAsync(relativePath, cancellationToken)
                ?? throw new NotFoundException("File Not Found");

            var stream = await _bucket.OpenDownloadStreamAsync(
                info.Id,
                new GridFSDownloadOptions { Seekable = true },
                cancellationToken);

            return new StoredFileStream
            {
                Stream = stream,
                Length = info.Length
            };
        }

        public async Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var info = await FindAsync(relativePath, cancellationToken);
            if (info == null) return false;

            await _bucket.DeleteAsync(info.Id, cancellationToken);
            return true;
        }

        public async Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
            => await FindAsync(relativePath, cancellationToken) != null;

        private async Task<GridFSFileInfo> FindAsync(string relativePath, CancellationToken cancellationToken)
        {
            var filter = Builders<GridFSFileInfo>.Filter.Eq(f => f.Filename, relativePath);
            var sort = Builders<GridFSFileInfo>.Sort.Descending(f => f.UploadDateTime);

            using var cursor = await _bucket.FindAsync(
                filter,
                new GridFSFindOptions { Limit = 1, Sort = sort },
                cancellationToken);

            return await cursor.FirstOrDefaultAsync(cancellationToken);
        }
    }
}
