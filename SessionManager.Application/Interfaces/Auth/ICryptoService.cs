namespace SessionManager.Application.Interfaces
{
    public interface ICryptoService
    {
        // Returns tuple: (CipherText, IV)
        (string CipherText, string IV) Encrypt(string plainText);

        string Decrypt(string cipherText, string iv);
    }
}