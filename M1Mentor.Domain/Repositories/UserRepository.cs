using M1Mentor.Domain.Collections;
using M1Mentor.Domain.Repositories.Contracts;
using MongoDB.Driver.Linq;
using Utilities.Exceptions.Common;
using Utilities.Extensions;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;
using static Utilities.Constants.RegisterMode;

namespace M1Mentor.Domain.Repositories
{
    public class UserRepository(IMonjoConnection connection) : MonjoRepository<User>(connection),
        IUserRepository, ISingletonDependency
    {
        public async Task<User> GetUserByPublicKeyAsync(string publicKey)
        {
            try
            {
                var user = await AsQueryable().FirstOrDefaultAsync(user => user.PublicKey == publicKey)
                    .CheckNotNullAsync("User not found");

                return user;
            }
            catch (BadRequestException ex)
            {
                throw new BadRequestException(ex.Message);
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }
    }
}