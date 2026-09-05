using System.IO.Pipelines;
using M1Mentor.Utilities.Services.Contracts;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Utilities.Exceptions.Common;
using Utilities.Extensions;

namespace Utilities.Services
{
    /// <summary>
    /// Shared implementation for object-store backends that speak the S3 protocol (AWS S3 and
    /// MinIO). Both uploads and downloads are fully streamed:
    /// <list type="bullet">
    ///   <item>Uploads hand the source stream straight to the SDK, which performs multipart upload
    ///   for large objects — the whole file is never materialised in memory.</item>
    ///   <item>Downloads bridge the SDK's push-based callback into a pull-based <see cref="Stream"/>
    ///   via <see cref="Pipe"/>, so a multi-GB object flows through with a bounded buffer instead of
    ///   being buffered into a <c>MemoryStream</c>.</item>
    /// </list>
    /// Concrete subclasses only supply the client, the provider key and the bucket name.
    /// </summary>
    public abstract class S3CompatibleStorageProviderBase : IFileStorageProvider
    {
        private readonly int _bufferSize;

        protected S3CompatibleStorageProviderBase(int streamBufferSize)
        {
            _bufferSize = streamBufferSize > 0 ? streamBufferSize : 81920;
        }

        public abstract string Name { get; }
        public abstract string BucketName { get; }

        /// <summary>Builds a configured client. Implementations decide endpoint / region / TLS.</summary>
        protected abstract IMinioClient CreateClient();

        public async Task<long> SaveAsync(Stream source, string relativePath, CancellationToken cancellationToken = default)
        {
            var objectKey = NormalizeKey(relativePath);
            var bucket = RequireBucket();
            var client = CreateClient();

            await EnsureBucketAsync(client, bucket, cancellationToken);

            // Known length when the source is seekable (the common case for uploads); otherwise -1
            // tells the SDK to stream with multipart and a default part size.
            var size = source.CanSeek ? source.Length : -1L;
            var contentType = FileTypeExtensions.GetMimeTypeFromFileExtension(Path.GetExtension(objectKey));

            var putArgs = new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey)
                .WithStreamData(source)
                .WithObjectSize(size)
                .WithContentType(contentType);

            try
            {
                var response = await client.PutObjectAsync(putArgs, cancellationToken);
                return response.Size > 0 ? response.Size : (source.CanSeek ? source.Length : 0);
            }
            catch (MinioException e)
            {
                throw new BaseException($"{Name} upload failed: {e.Message}");
            }
        }

        public async Task<StoredFileStream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var objectKey = NormalizeKey(relativePath);
            var bucket = RequireBucket();
            var client = CreateClient();

            long length;
            try
            {
                var stat = await client.StatObjectAsync(
                    new StatObjectArgs().WithBucket(bucket).WithObject(objectKey), cancellationToken);
                length = stat.Size;
            }
            catch (ObjectNotFoundException)
            {
                throw new NotFoundException("File Not Found");
            }
            catch (MinioException e)
            {
                throw new BaseException($"{Name} stat failed: {e.Message}");
            }

            // Bridge the push-based GetObject callback into a readable stream without buffering the
            // whole payload. The producer copies object bytes into the pipe; the caller reads them.
            var pipe = new Pipe();
            var producerStream = pipe.Writer.AsStream();

            _ = Task.Run(async () =>
            {
                try
                {
                    var getArgs = new GetObjectArgs()
                        .WithBucket(bucket)
                        .WithObject(objectKey)
                        .WithCallbackStream(async (stream, ct) =>
                            await stream.CopyToAsync(producerStream, _bufferSize, ct));

                    await client.GetObjectAsync(getArgs, cancellationToken);
                    await pipe.Writer.CompleteAsync();
                }
                catch (Exception ex)
                {
                    await pipe.Writer.CompleteAsync(ex);
                }
            }, cancellationToken);

            return new StoredFileStream
            {
                Stream = pipe.Reader.AsStream(),
                Length = length
            };
        }

        public async Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var objectKey = NormalizeKey(relativePath);
            var bucket = RequireBucket();
            var client = CreateClient();

            if (!await ExistsAsync(relativePath, cancellationToken))
                return false;

            try
            {
                await client.RemoveObjectAsync(
                    new RemoveObjectArgs().WithBucket(bucket).WithObject(objectKey), cancellationToken);
                return true;
            }
            catch (MinioException e)
            {
                throw new BaseException($"{Name} delete failed: {e.Message}");
            }
        }

        public async Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var objectKey = NormalizeKey(relativePath);
            var bucket = RequireBucket();
            var client = CreateClient();

            try
            {
                await client.StatObjectAsync(
                    new StatObjectArgs().WithBucket(bucket).WithObject(objectKey), cancellationToken);
                return true;
            }
            catch (ObjectNotFoundException)
            {
                return false;
            }
            catch (MinioException e)
            {
                throw new BaseException($"{Name} existence check failed: {e.Message}");
            }
        }

        #region Helpers

        private static async Task EnsureBucketAsync(IMinioClient client, string bucket, CancellationToken cancellationToken)
        {
            var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), cancellationToken);
            if (!exists)
                await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), cancellationToken);
        }

        private string RequireBucket()
            => string.IsNullOrWhiteSpace(BucketName)
                ? throw new BaseException($"{Name} bucket name is not configured")
                : BucketName;

        /// <summary>Object keys use forward slashes and never a leading slash.</summary>
        private static string NormalizeKey(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new BadRequestException("Storage path is required");

            return relativePath.Replace('\\', '/').TrimStart('/');
        }

        #endregion
    }
}
