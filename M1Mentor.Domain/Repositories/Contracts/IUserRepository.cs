using M1Mentor.Domain.Collections;
using Utilities.MongoDatabase.Contracts;

namespace M1Mentor.Domain.Repositories.Contracts
{
    public interface IUserRepository : IMonjoRepository<User>
    {
        Task<User> GetUserByPublicKeyAsync(string publicKey);
    }
}