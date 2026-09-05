using M1Mentor.Utilities.Services.Contracts;
using Minio;
using Utilities.Exceptions.Common;
using Utilities.Models.Settings;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    /// <summary>
    /// MinIO backed <see cref="IFileStorageProvider"/>. Connects to a self-hosted MinIO
    /// endpoint and inherits the fully-streamed upload/download behaviour from
    /// <see cref="S3CompatibleStorageProviderBase"/> so multi-GB objects move with bounded memory.
    /// </summary>
    public class MinioStorageProvider(FileStorageSettings _settings)
        : S3CompatibleStorageProviderBase(_settings.StreamBufferSize), ISelfSingletonDependency
    {
        private MinioStorageOptions Options => _settings.MinIO;

        public override string Name => "MinIO";

        public override string BucketName => Options.BucketName;

        protected override IMinioClient CreateClient()
        {
            var options = Options;

            var builder = new MinioClient()
                .WithEndpoint(options.Endpoint?.Trim() ?? throw new BadRequestException("MinIO Endpoint is required"))
                .WithCredentials(
                    options.AccessKey?.Trim() ?? throw new BadRequestException("MinIO AccessKey is required"),
                    options.SecretKey?.Trim() ?? throw new BadRequestException("MinIO SecretKey is required"));

            if (options.UseSSL)
                builder = builder.WithSSL();

            return builder.Build();
        }
    }
}
