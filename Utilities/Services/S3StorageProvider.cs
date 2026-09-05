using M1Mentor.Utilities.Services.Contracts;
using Minio;
using Utilities.Exceptions.Common;
using Utilities.Models.Settings;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    /// <summary>
    /// AWS S3 backed <see cref="IFileStorageProvider"/>. Connects with the S3 protocol
    /// (region-aware, TLS) and inherits the fully-streamed upload/download behaviour from
    /// <see cref="S3CompatibleStorageProviderBase"/> so multi-GB objects move with bounded memory.
    /// </summary>
    public class S3StorageProvider(FileStorageSettings _settings)
        : S3CompatibleStorageProviderBase(_settings.StreamBufferSize), ISelfSingletonDependency
    {
        private S3StorageOptions Options => _settings.S3;

        public override string Name => "S3";

        public override string BucketName => Options.BucketName;

        protected override IMinioClient CreateClient()
        {
            var options = Options;

            var builder = new MinioClient()
                .WithEndpoint(options.Endpoint?.Trim() ?? throw new BadRequestException("S3 Endpoint is required"))
                .WithCredentials(
                    options.AccessKey?.Trim() ?? throw new BadRequestException("S3 AccessKey is required"),
                    options.SecretKey?.Trim() ?? throw new BadRequestException("S3 SecretKey is required"));

            if (!string.IsNullOrWhiteSpace(options.Region))
                builder = builder.WithRegion(options.Region.Trim());

            if (options.UseSSL)
                builder = builder.WithSSL();

            return builder.Build();
        }
    }
}
