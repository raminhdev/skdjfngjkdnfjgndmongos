using M1Mentor.Utilities.Services.Contracts;
using Utilities.Exceptions.Common;
using Utilities.Models.Settings;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    /// <summary>
    /// The injectable <see cref="IFileStorageProvider"/>. It selects the concrete backend named in
    /// <see cref="FileStorageSettings.Provider"/> at resolution time and forwards every call to it.
    /// This is what makes storage swappable purely through configuration — consumers (e.g.
    /// FileMetaService) inject <see cref="IFileStorageProvider"/> and never know which backend runs.
    /// </summary>
    public class FileStorageProviderResolver : IFileStorageProvider, ISingletonDependency
    {
        private readonly IFileStorageProvider _active;

        public FileStorageProviderResolver(
            FileStorageSettings settings,
            LocalStorageProvider local,
            GridFsStorageProvider gridFs,
            S3StorageProvider s3,
            MinioStorageProvider minio)
        {
            _active = SelectProvider(settings, local, gridFs, s3, minio);
        }

        /// <summary>
        /// Configuration-driven backend selection. An explicit <see cref="FileStorageSettings.Provider"/>
        /// always wins. When it is empty or "Auto", the documented priority applies:
        /// S3 (if configured) → MinIO (if configured) → Local fallback.
        /// </summary>
        private static IFileStorageProvider SelectProvider(
            FileStorageSettings settings,
            LocalStorageProvider local,
            GridFsStorageProvider gridFs,
            S3StorageProvider s3,
            MinioStorageProvider minio)
        {
            var provider = (settings.Provider ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(provider) || provider.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                if (settings.S3 is { IsConfigured: true }) return s3;
                if (settings.MinIO is { IsConfigured: true }) return minio;
                return local;
            }

            return provider.ToLowerInvariant() switch
            {
                "local" => local,
                "gridfs" => gridFs,
                "s3" => s3,
                "minio" => minio,
                var other => throw new BaseException(
                    $"Unknown file storage provider: '{other}'. Supported: Local, GridFs, S3, MinIO.")
            };
        }

        public string Name => _active.Name;

        public string BucketName => _active.BucketName;

        public Task<long> SaveAsync(Stream source, string relativePath, CancellationToken cancellationToken = default)
            => _active.SaveAsync(source, relativePath, cancellationToken);

        public Task<StoredFileStream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
            => _active.OpenReadAsync(relativePath, cancellationToken);

        public Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
            => _active.DeleteAsync(relativePath, cancellationToken);

        public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
            => _active.ExistsAsync(relativePath, cancellationToken);
    }
}
