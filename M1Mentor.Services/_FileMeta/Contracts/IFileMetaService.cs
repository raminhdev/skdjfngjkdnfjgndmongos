using M1Mentor.Utilities.Services;
using M1Mentor.Domain.Collections;
using M1Mentor.Services._FileMeta.DTOs.Results;
using Microsoft.AspNetCore.Http;
using Utilities.MongoDatabase.Filter;
using Utilities.Services;

namespace M1Mentor.Services._FileMeta.Contracts
{
    //Used in Endpoints / controllers
    public interface IFileMetaService
    {
        Task<string> UploadFileAsync(IFormFile file, EntityType type, string uploadedBy = null);
        Task<FileDataResult> DownloadFileAsync(string storedFileName);
        Task<bool> DeleteFileAsync(string fileMetaId);
        Task<MonjoFilteredResult<FileMetaResult>> GetAllFilesAsync(MonjoQuery monjoQuery);
    }
}