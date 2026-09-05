using System.Text;
using M1Mentor.Utilities.Services.Contracts;
using Microsoft.AspNetCore.Http;
using Minio;
using Minio.ApiEndpoints;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Utilities.Exceptions.Common;
using Utilities.Extensions;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    //simple storage service = s3 compatible storage
    public class S3FileService(S3Settings _s3Settings) : IS3FileService, IScopedDependency
    {
        public IMinioClient BuildMinioClient()
        {
            try
            {
                return new MinioClient()
                 .WithEndpoint(_s3Settings.Endpoint)
                 .WithCredentials(
                     _s3Settings.AccessKey?.Trim() ?? throw new BadRequestException("AccessKey is required"),
                     _s3Settings.SecretKey?.Trim() ?? throw new BadRequestException("SecretKey is required")
                 )
                 .WithSSL()
                 .Build();
            }
            catch (BadRequestException e)
            {
                throw new BaseException(e.Message);
            }
            catch (Exception)
            {
                throw new BaseException("error in minio client!");
            }

        }


        public async Task<List<Bucket>> ListBucketsAsync()
        {
            var client = BuildMinioClient();
            var result = await client.ListBucketsAsync();
            return result.Buckets.ToList();
        }

        public async Task<string> GetBucketObjectsAsync()
        {
            var client = BuildMinioClient();
            var bucketName = _s3Settings.BucketName;

            bool found = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName));

            if (!found)
                throw new NotFoundException($"Bucket '{bucketName}' does not exist.");

            ListObjectsArgs args = new ListObjectsArgs()
                .WithBucket(bucketName)
                .WithRecursive(true);

            var observable = client.ListObjectsAsync(args);

            var log = new StringBuilder();
            var done = new TaskCompletionSource<bool>();

            IDisposable subscription = observable.Subscribe(
                item => log.AppendLine($"OnNext: {item.Key}"),
                ex =>
                {
                    log.AppendLine($"OnError: {ex.Message}");
                    done.SetResult(true);
                },
                () => done.SetResult(true)
            );

            await done.Task;

            subscription.Dispose();

            return log.ToString();
        }
        public async Task<string> UploadFileAsync(string discUri, string fileUri)
        {
            var bucketName = _s3Settings.BucketName ?? throw new BadRequestException("Bucket name is required");
            var client = BuildMinioClient();

            if (!File.Exists(discUri))
                throw new NotFoundException($"File not found at path: {discUri}");

            var fileName = Path.GetFileName(fileUri);
            var objectName = fileUri;
            var contentType = FileTypeExtensions.GetMimeTypeFromFileExtension(Path.GetExtension(fileName));

            try
            {
                using var fileStream = new FileStream(discUri, FileMode.Open, FileAccess.Read, FileShare.Read);
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithStreamData(fileStream)
                    .WithObjectSize(fileStream.Length)
                    .WithContentType(contentType);

                await client.PutObjectAsync(putObjectArgs);

                return $"{bucketName}/{objectName}";
            }
            catch (MinioException e)
            {
                throw new BaseException($"MinIO Upload Failed: {e.Message}");
            }
        }
        //public async Task<string> UploadFileAsync(string discUri, string fileUri)
        //{
           
        //    var bucketName = _s3Settings.BucketName ?? throw new BadRequestException("Bucket name is required");
        //    var client = BuildMinioClient();

        //    if (!File.Exists(discUri))
        //        throw new NotFoundException($"File not found at path: {discUri}");

        //    var fileName = Path.GetFileName(fileUri);
        //    var objectName = fileUri; 
        //    var contentType = FileTypeExtensions.GetMimeTypeFromFileExtension(Path.GetExtension(fileName)); 

        //    try
        //    {
        //        var putObjectArgs = new PutObjectArgs()
        //            .WithBucket(bucketName)
        //            .WithObject(objectName)
        //            .WithFileName(discUri)
        //            .WithContentType(contentType);

        //        await client.PutObjectAsync(putObjectArgs);

        //        return $"{bucketName}/{objectName}";
        //    }
        //    catch (MinioException e)
        //    {
        //        throw new BaseException($"MinIO Upload Failed: {e.Message}");
        //    }
        //}

        public async Task<bool> ConfirmFileUploadedAsync(string fullPath)
        {
            var objectName = ValidatePath(fullPath);
            var bucketName = _s3Settings.BucketName ?? throw new BadRequestException("Bucket name is required");

            var client = BuildMinioClient();

            try
            {
                var statArgs = new StatObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName);

                await client.StatObjectAsync(statArgs);
                return true; // file exists
            }
            catch (ObjectNotFoundException)
            {
                return false; // file not found
            }
            catch (MinioException e)
            {
                throw new BaseException($"Failed to confirm upload: {e.Message}");
            }
        }

        public async Task<FileDataResult> DownloadFileAsync(string fullPath)
        {
            var objectName = ValidatePath(fullPath);
            var bucketName = _s3Settings.BucketName ?? throw new BadRequestException("Bucket name is required");

            var client = BuildMinioClient();

            try
            {
                MemoryStream memoryStream = new MemoryStream();

                var getObjectArgs = new GetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithCallbackStream(stream =>
                    {
                        stream.CopyTo(memoryStream);
                    });

                await client.GetObjectAsync(getObjectArgs);

                memoryStream.Position = 0; // Reset stream position for reading

                var fileName = Path.GetFileName(fullPath);
                var contentType = FileTypeExtensions.GetMimeTypeFromFileExtension(Path.GetExtension(fileName));

                return new FileDataResult
                { 
                    Stream = memoryStream,
                    FileName = fileName,
                    ContentType = contentType
                };
            }
            catch (ObjectNotFoundException)
            {
                throw new NotFoundException($"Object not found in bucket: {bucketName}/{objectName}");
            }
            catch (MinioException e)
            {
                throw new BaseException($"Failed to download file from MinIO: {e.Message}");
            }
        }

        
        private string ValidatePath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new BadRequestException("Path cannot be null or empty.", nameof(fullPath));

            fullPath = fullPath.Trim();
            
            fullPath = fullPath.Replace(":", "");
            fullPath = fullPath.Replace("\\", "/").TrimStart('/');

            return fullPath.RemoveSpaces();
        }


        //public async Task UploadFileAsync(FileData fileData, string folderName = "test")
        //{

        //    try
        //    {
        //        var client = BuildMinioClient();

        //        string bucketName = _s3Settings.BucketName;
        //        string objectName = string.IsNullOrEmpty(folderName)
        //            ? fileData.File.FileName
        //            : $"{folderName.TrimEnd('/')}/{fileData.File.FileName}";

        //        // MIME type
        //        //var provider = new FileExtensionContentTypeProvider();
        //        //if (!provider.TryGetContentType(fileData.File.FileName, out var contentType))
        //        //    contentType = "application/octet-stream";
        //        var contentType = fileData.File.ContentType;
        //        // IMPORTANT: Copy file to memory stream and get accurate size
        //        await using var memoryStream = new MemoryStream();
        //        await fileData.File.CopyToAsync(memoryStream);
        //        memoryStream.Seek(0, SeekOrigin.Begin); // reset position
        //        var size = memoryStream.Length;

        //        Console.WriteLine($"⏫ Uploading file: {objectName}");
        //        Console.WriteLine($"📦 Content-Type: {contentType}");
        //        Console.WriteLine($"📏 Size: {size} bytes");

        //        var putArgs = new PutObjectArgs()
        //            .WithBucket(bucketName)
        //            .WithObject(objectName)
        //            .WithStreamData(memoryStream)
        //            .WithObjectSize(size)
        //            .WithContentType(contentType);

        //        await client.PutObjectAsync(putArgs);
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new BaseException("Error in uploading file to S3", ex);
        //    }
        //}


        //public async Task UploadxFileAsync(FileData fileData, string folderName = "common")
        //{
        //    try
        //    {
        //        var client = BuildMinioClient();

        //        string objectName = string.IsNullOrEmpty(folderName)
        //            ? fileData.File.FileName
        //            : $"{folderName.TrimEnd('/')}/{fileData.File.FileName}";

        //        string bucketName = _s3Settings.BucketName;



        //        //using var stream = fileData.File.OpenReadStream();
        //        await using var memoryStream = new MemoryStream();
        //        await fileData.File.CopyToAsync(memoryStream);
        //        memoryStream.Position = 0;

        //        var putArgs = new PutObjectArgs()
        //            .WithBucket(bucketName)
        //            .WithObject(objectName)
        //            .WithStreamData(memoryStream)
        //            .WithObjectSize(memoryStream.Length)
        //            .WithContentType(fileData.File.ContentType ?? "application/octet-stream");

        //        await client.PutObjectAsync(putArgs);
        //    }
        //    catch (Exception ex)
        //    {
        //        // Add logging here if needed
        //        throw new BaseException("Error in uploading file to S3", ex);
        //    }
        //}



        //public async Task<string> UploadFileAsync(string fullPath)
        //{
        //    var clearPath = ValidatePath(fullPath);

        //    var bucketName = _s3Settings.BucketName ?? throw new BadRequestException("Bucket name is required");

        //    var client = BuildMinioClient();

        //    if (!File.Exists(fullPath))
        //        throw new NotFoundException($"File not found at path: {fullPath}");

        //    var fileName = Path.GetFileName(clearPath);
        //    var objectName = clearPath; // full path inside the bucket

        //    try
        //    {
        //        await using var fileStream = File.OpenRead(fullPath);

        //        var putObjectArgs = new PutObjectArgs()
        //            .WithBucket(bucketName)
        //            .WithObject(objectName)
        //            .WithStreamData(fileStream)
        //            .WithObjectSize(fileStream.Length)
        //            .WithContentType(MimeTypes.GetMimeType(fileName));

        //        await client.PutObjectAsync(putObjectArgs);

        //        return $"{bucketName}/{objectName}";
        //    }
        //    catch (MinioException e)
        //    {
        //        throw new BaseException($"MinIO Upload Failed: {e.Message}");
        //    }
        //}

    }







    public class S3Settings : ISingletonDependency
    {
        public string Endpoint { get; set; }
        public string AccessKey { get; set; }
        public string SecretKey { get; set; }
        public string BucketName { get; set; }

    }

    public record FileData
    {
        //public string FileName { get; set; }
        public IFormFile File { get; set; }
    }

    public class FileDataResult
    {
        public string ContentType { get; set; }
        public Stream Stream { get; set; }
        public string FileName { get; set; }

    }

}
