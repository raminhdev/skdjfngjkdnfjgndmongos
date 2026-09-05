namespace Utilities.Enums
{
    public enum Claims
    {
        PublicKey,
        Permission,
        SecurityStamp,
        Role,
        FullName,
        UserType,
        PhoneNumber,

        WalletAddress
    }

    public enum UserType
    {
        User,
        Customer,
        Inspector,

        Admin,
    }
}