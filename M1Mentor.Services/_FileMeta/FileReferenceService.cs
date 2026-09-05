using M1Mentor.Domain.Collections;
using M1Mentor.Domain.Repositories.Contracts;
using M1Mentor.Services._FileMeta.Contracts;
using M1Mentor.Services._FileMeta.DTOs.Results;
using MongoDB.Driver.Linq;
using Utilities.Exceptions.Common;
using Utilities.MongoDatabase.Extensions;
using Utilities.MongoDatabase.Filter;
using static Utilities.Constants.RegisterMode;

namespace M1Mentor.Services._FileMeta
{
    public class FileReferenceService(IFileReferenceRepository _fileReferenceRepository)
        : IScopedDependency, IFileReferenceService
    {
        public async Task<FileReference> CreateAsync(string fileMetaId, EntityType type, string entityId = null)
        {
            var reference = new FileReference
            {
                FileMetaId = fileMetaId,
                Type = type,
                EntityId = entityId
            };

            await _fileReferenceRepository.InsertOneAsync(reference);
            return reference;
        }


        public async Task DeleteAsync(string referenceId)
        {
            await _fileReferenceRepository.DeleteOneAsync(r => r.ReferenceId == referenceId);
        }


        public async Task<List<FileReference>> GetByEntityAsync(string entityId, EntityType type)
        {
            return await _fileReferenceRepository.AsQueryable()
                .Where(r => r.EntityId == entityId && r.Type == type)
                .ToListAsync();
        }


        public async Task<List<FileReference>> GetByFileMetaIdAsync(string fileMetaId)
        {
            return await _fileReferenceRepository.AsQueryable()
                .Where(r => r.FileMetaId == fileMetaId)
                .ToListAsync();
        }


        public async Task<long> CountByFileMetaIdAsync(string fileMetaId)
        {
            return await _fileReferenceRepository.AsQueryable()
                .Where(r => r.FileMetaId == fileMetaId)
                .CountAsync();
        }


        public async Task<MonjoFilteredResult<FileReferenceResult>> GetAllAsync(MonjoQuery monjoQuery)
        {
            monjoQuery.WithBase<FileReferenceResult>();

            return await _fileReferenceRepository.AsQueryable()
                .Apply(monjoQuery.Where)
                .Apply(monjoQuery.Order)
                .Select(r => new FileReferenceResult
                {
                    CreatedMoment = r.CreatedMoment,
                    EntityId = r.EntityId,
                    FileMetaId = r.FileMetaId,
                    ReferenceId = r.ReferenceId,
                    Type = r.Type,
                    CreatedByInfo = r.CreatedByInfo,
                    ModifiedByInfo = r.ModifiedByInfo,
                    ModifiedMoment = r.ModifiedMoment
                })
                .ExecuteAsync(monjoQuery);
        }

        public async Task<int> GetFileLiveCountAsync(FileMeta file)
            => await _fileReferenceRepository.AsQueryable()
                .Where(r => r.FileMetaId == file.FileMetaId)
                .CountAsync();

        #region PrivaeMethods

        public async Task<FileReference> GetFileReferenceByIdAsync(string fileMetaId)
            => await _fileReferenceRepository.AsQueryable().FirstOrDefaultAsync(q => q.FileMetaId == fileMetaId)
               ?? throw new NotFoundException("Could not Find FileReference");

        public async Task<FileReference> GetFileMetaByStoredName(string referenceId)
            => await _fileReferenceRepository.AsQueryable().FirstOrDefaultAsync(q => q.ReferenceId == referenceId)
               ?? throw new NotFoundException("Could not Find FileReference");

        public async Task<FileReference> GetFileMetaByStoragePath(string enatityId)
            => await _fileReferenceRepository.AsQueryable().FirstOrDefaultAsync(q => q.EntityId == enatityId)
               ?? throw new NotFoundException("Could not Find FileReference");

        #endregion
    }
}