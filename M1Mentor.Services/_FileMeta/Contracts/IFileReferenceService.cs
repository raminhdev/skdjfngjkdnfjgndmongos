using M1Mentor.Domain.Collections;
using M1Mentor.Services._FileMeta.DTOs.Results;
using Utilities.MongoDatabase.Filter;

namespace M1Mentor.Services._FileMeta.Contracts
{
    public interface IFileReferenceService
    {
        Task<FileReference> CreateAsync(string fileMetaId, EntityType type, string entityId = null);
        Task DeleteAsync(string referenceId);
        Task<List<FileReference>> GetByEntityAsync(string entityId, EntityType type);
        Task<List<FileReference>> GetByFileMetaIdAsync(string fileMetaId);
        Task<long> CountByFileMetaIdAsync(string fileMetaId);
        Task<MonjoFilteredResult<FileReferenceResult>> GetAllAsync(MonjoQuery monjoQuery);
        Task<int> GetFileLiveCountAsync(FileMeta file);
    }
}