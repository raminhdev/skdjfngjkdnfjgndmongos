using M1Mentor.Utilities.Services.Contracts;
using Utilities.Exceptions.Common;
using Utilities.Models.Settings;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    /// <summary>
    /// Disk-backed <see cref="IFileStorageProvider"/>. Writes and reads are fully streamed with a
    /// bounded buffer, so files of any size (multi-GB) move through with constant memory. All
    /// relative paths are resolved underneath the configured root and validated against path
    /// traversal so a crafted name can never escape the storage root.
    /// </summary>
    public class LocalStorageProvider(FileStorageSettings _settings) : IFileStorageProvider, ISelfSingletonDependency
    {
        public string Name => "Local";

        public string BucketName => null;

        public async Task<long> SaveAsync(Stream source, string relativePath, CancellationToken cancellationToken = default)
        {
            var fullPath = ResolveFullPath(relativePath);

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await using var destination = new FileStream(
                fullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: _settings.StreamBufferSize,
                useAsync: true);

            await source.CopyToAsync(destination, _settings.StreamBufferSize, cancellationToken);
            await destination.FlushAsync(cancellationToken);

            return destination.Length;
        }

        public Task<StoredFileStream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var fullPath = ResolveFullPath(relativePath);

            if (!File.Exists(fullPath))
                throw new NotFoundException("File Not Found");

            var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: _settings.StreamBufferSize,
                useAsync: true);

            return Task.FromResult(new StoredFileStream
            {
                Stream = stream,
                Length = stream.Length
            });
        }

        public Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var fullPath = ResolveFullPath(relativePath);

            if (!File.Exists(fullPath))
                return Task.FromResult(false);

            File.Delete(fullPath);
            return Task.FromResult(true);
        }

        public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
            => Task.FromResult(File.Exists(ResolveFullPath(relativePath)));

        /// <summary>
        /// Combines the relative path with the configured root and guarantees the result stays
        /// inside the root (path-traversal protection).
        /// </summary>
        private string ResolveFullPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new BadRequestException("Storage path is required");

            if (string.IsNullOrWhiteSpace(_settings.LocalRootPath))
                throw new BadRequestException("Local storage root path is not configured (FileStorage:LocalRootPath)");

            var root = Path.GetFullPath(_settings.LocalRootPath);
            var combined = Path.GetFullPath(Path.Combine(root, relativePath));

            var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;

            if (!combined.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Invalid storage path");

            return combined;
        }
    }
}
