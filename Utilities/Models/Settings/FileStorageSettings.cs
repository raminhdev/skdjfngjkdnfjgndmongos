namespace Utilities.Models.Settings
{
    /// <summary>
    /// Central configuration for the file storage subsystem. All limits and paths are
    /// configuration-driven (no hardcoded values) so storage can be tuned per environment
    /// and the backing provider swapped without code changes.
    /// </summary>
    public class FileStorageSettings
    {
        /// <summary>
        /// Active storage backend. Supported values: "Local", "GridFs", "S3", "MinIO".
        /// When left empty or "Auto", the resolver auto-selects by priority:
        /// S3 (if configured) → MinIO (if configured) → Local fallback.
        /// </summary>
        public string Provider { get; set; } = "Local";

        /// <summary>AWS S3 configuration. Used when <see cref="Provider"/> is "S3".</summary>
        public S3StorageOptions S3 { get; set; } = new();

        /// <summary>MinIO configuration. Used when <see cref="Provider"/> is "MinIO".</summary>
        public MinioStorageOptions MinIO { get; set; } = new();

        /// <summary>
        /// Root directory for the local provider. Stored file paths are kept relative to this
        /// root so the physical location can move between environments.
        /// </summary>
        public string LocalRootPath { get; set; }

        /// <summary>
        /// Maximum accepted upload size, expressed in gigabytes. Drives Kestrel / multipart /
        /// form limits and per-request validation. 0 or negative means "use the framework default".
        /// </summary>
        public double MaxUploadSizeGB { get; set; } = 10;

        /// <summary>GridFS bucket name when <see cref="Provider"/> is "GridFs".</summary>
        public string GridFsBucketName { get; set; } = "files";

        /// <summary>Buffer size, in bytes, used for streaming copies. Defaults to 80 KB.</summary>
        public int StreamBufferSize { get; set; } = 81920;

        public long MaxUploadSizeBytes =>
            MaxUploadSizeGB > 0 ? (long)(MaxUploadSizeGB * 1024L * 1024L * 1024L) : long.MaxValue;
    }

    /// <summary>AWS S3 connection options (S3-compatible client).</summary>
    public class S3StorageOptions
    {
        /// <summary>S3 endpoint host. Defaults to AWS global endpoint.</summary>
        public string Endpoint { get; set; } = "s3.amazonaws.com";
        public string Region { get; set; }
        public string AccessKey { get; set; }
        public string SecretKey { get; set; }
        public string BucketName { get; set; }

        /// <summary>Use TLS (HTTPS). AWS S3 requires this; defaults to true.</summary>
        public bool UseSSL { get; set; } = true;

        /// <summary>True when AccessKey, SecretKey and BucketName are all present.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(AccessKey) &&
            !string.IsNullOrWhiteSpace(SecretKey) &&
            !string.IsNullOrWhiteSpace(BucketName);
    }

    /// <summary>MinIO connection options.</summary>
    public class MinioStorageOptions
    {
        /// <summary>MinIO endpoint as host or host:port (no scheme).</summary>
        public string Endpoint { get; set; }
        public string AccessKey { get; set; }
        public string SecretKey { get; set; }
        public string BucketName { get; set; }

        /// <summary>Use TLS (HTTPS) against the MinIO endpoint. Defaults to false for local clusters.</summary>
        public bool UseSSL { get; set; } = false;

        /// <summary>True when Endpoint, AccessKey, SecretKey and BucketName are all present.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Endpoint) &&
            !string.IsNullOrWhiteSpace(AccessKey) &&
            !string.IsNullOrWhiteSpace(SecretKey) &&
            !string.IsNullOrWhiteSpace(BucketName);
    }
}
