namespace Utilities.Services.Contracts
{
    public interface INonceService
    {
        //void Add(string item);
        //bool Contains(string item);
        bool TryUse(string nonce, TimeSpan ttl);
    }
}
