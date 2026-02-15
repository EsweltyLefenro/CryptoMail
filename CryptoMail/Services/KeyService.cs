using System.IO;
using System.Security.Cryptography;
using CryptoMail.Models;

namespace CryptoMail.Services;

public sealed class KeyService
{
    public KeyPaths GetDefaultKeyPaths(string appBaseDir)
    {
        var keysDir = Path.Combine(appBaseDir, "Keys");
        Directory.CreateDirectory(keysDir);

        return new KeyPaths
        {
            SenderPrivatePath = Path.Combine(keysDir, "sender_private.pem"),
            SenderPublicPath = Path.Combine(keysDir, "sender_public.pem"),
            RecipientPrivatePath = Path.Combine(keysDir, "recipient_private.pem"),
            RecipientPublicPath = Path.Combine(keysDir, "recipient_public.pem")
        };
    }

    public void GenerateSenderKeys(KeyPaths keyPaths) => GeneratePair(keyPaths.SenderPrivatePath, keyPaths.SenderPublicPath);

    public void GenerateRecipientKeys(KeyPaths keyPaths) => GeneratePair(keyPaths.RecipientPrivatePath, keyPaths.RecipientPublicPath);

    public string LoadPem(string path) => File.ReadAllText(path);

    public void SavePem(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, text);
    }

    public bool AllKeysExist(KeyPaths keyPaths) =>
        File.Exists(keyPaths.SenderPrivatePath) &&
        File.Exists(keyPaths.SenderPublicPath) &&
        File.Exists(keyPaths.RecipientPrivatePath) &&
        File.Exists(keyPaths.RecipientPublicPath);

    private void GeneratePair(string privatePath, string publicPath)
    {
        using var rsa = RSA.Create(3072);
        SavePem(privatePath, rsa.ExportRSAPrivateKeyPem());
        SavePem(publicPath, rsa.ExportRSAPublicKeyPem());
    }
}
