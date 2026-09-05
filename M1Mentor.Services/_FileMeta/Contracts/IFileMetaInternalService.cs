using M1Mentor.Domain.Collections;

namespace M1Mentor.Services._FileMeta.Contracts
{
    //Used in Entity Services
    public interface IFileMetaInternalService
    {
        Task SyncEntityFilesAsync(string entityId, EntityType type, IEnumerable<string> currentStoredNames);
    }
}
