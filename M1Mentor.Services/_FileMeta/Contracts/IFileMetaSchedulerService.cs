namespace M1Mentor.Services._FileMeta.Contracts
{
    //Used in Schedulers
    public interface IFileMetaSchedulerService
    {
        Task CleanupMarkedFilesAsync();
        Task MarkFilesForDeletionAsync();
    }
}
