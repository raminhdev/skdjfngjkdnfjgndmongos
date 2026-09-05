using Utilities.Extensions;
using Utilities.Services.Contracts;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    public class PasswordService : IPasswordService, ISingletonDependency
    {
        public string Hash(string password)
            => BCrypt.Net.BCrypt.HashPassword(password);

        public bool Verify(string password, string passwordHash)
        {
            password.NotNull(nameof(password));
            passwordHash.NotNull(nameof(password));
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
    }
}