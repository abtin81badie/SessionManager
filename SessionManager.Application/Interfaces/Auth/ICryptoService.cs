namespace SessionManager.Application.Interfaces
{
    public interface ICryptoService
    {
        (string CipherText, string IV) Encrypt(string plainText);

        string Decrypt(string cipherText, string iv);
    }
}