namespace Utilities.Services.Contracts
{
    public interface ISignatureService
    {
        byte[] Sign(byte[] key, byte[] data);
        bool Verify(byte[] key, byte[] data, byte[] signature);
    }
}
