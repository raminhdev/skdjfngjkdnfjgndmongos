using System.Security.Cryptography;
using System.Text;

namespace Utilities.Utilities
{
    //TODO : make this inject setting and read from there
    public static class EncryptionHelper
    {
        public static class AESEncryptionHelper
        {
            private static readonly string Key = "Your32CharLongEncryptionKey!"; // 32 chars for AES-256
            private static readonly string IV = "Your16CharLongIV!"; // 16 chars for AES block size

            public static string AESEncrypt(string plainText)
            {
                using var aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(Key);
                aes.IV = Encoding.UTF8.GetBytes(IV);

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(plainText);
                }

                return Convert.ToBase64String(ms.ToArray());
            }

            public static string AESDecrypt(string cipherText)
            {
                using var aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(Key);
                aes.IV = Encoding.UTF8.GetBytes(IV);

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);

                return sr.ReadToEnd();
            }
        }


    }
}
