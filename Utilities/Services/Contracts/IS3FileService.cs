using Minio.DataModel;
using Utilities.Services;

namespace M1Mentor.Utilities.Services.Contracts
{
    public interface IS3FileService
    {
        Task<List<Bucket>> ListBucketsAsync();
        Task<string> GetBucketObjectsAsync();
        Task<string> UploadFileAsync(string discUri,string fileUri);
        Task<bool> ConfirmFileUploadedAsync(string fullPath);
        Task<FileDataResult> DownloadFileAsync(string fullPath);
    }
}
