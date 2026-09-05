using M1Mentor.Domain.Collections;
using M1Mentor.Domain.Repositories.Contracts;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;
using static Utilities.Constants.RegisterMode;


namespace M1Mentor.Domain.Repositories
{
    public class RequestLogRepository(IMonjoConnection connection)
        : MonjoRepository<RequestLog>(connection), IRequestLogRepository, ISingletonDependency
    {
    }
}
