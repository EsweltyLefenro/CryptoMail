using System.Security.Cryptography;
using CryptoMail.Models;
using System.IO;

namespace CryptoMail.Services;

public sealed class KeyService
{
    public KeyPaths GetDefaultKeyPaths(string? appBaseDir = null)
    {
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoMail");
        string keysDir = Path.Combine(appData, "Keys");
        string recipientsDir = Path.Combine(appData, "Recipients");
        string trustedSendersDir = Path.Combine(appData, "TrustedSenders");
        Directory.CreateDirectory(keysDir);
        Directory.CreateDirectory(recipientsDir);
        Directory.CreateDirectory(trustedSendersDir);

        return new KeyPaths
        {
            SenderPrivatePath = Path.Combine(keysDir, "sender_private.pem"),
            SenderPublicPath = Path.Combine(keysDir, "sender_public.pem"),
            RecipientPrivatePath = Path.Combine(keysDir, "recipient_private.pem"),
            RecipientPublicPath = Path.Combine(keysDir, "recipient_public.pem"),
            PartnerRecipientPublicPath = Path.Combine(recipientsDir, "partner_recipient_public.pem"),
            TrustedSenderPublicPath = Path.Combine(trustedSendersDir, "trusted_sender.pem")
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

    public bool SenderKeysExist(KeyPaths keyPaths) =>
        File.Exists(keyPaths.SenderPrivatePath) &&
        File.Exists(keyPaths.SenderPublicPath);

    public bool RecipientPrivateKeyExists(KeyPaths keyPaths)
        => File.Exists(keyPaths.RecipientPrivatePath);

    public bool PublicKeyExists(string path)
        => File.Exists(path);

    private void GeneratePair(string privatePath, string publicPath)
    {
        using var rsa = RSA.Create(3072);
        SavePem(privatePath, rsa.ExportRSAPrivateKeyPem());
        SavePem(publicPath, rsa.ExportRSAPublicKeyPem());
    }

    public string GetFingerprint(string pem)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            var pubKey = rsa.ExportRSAPublicKey();
            var hash = SHA256.HashData(pubKey);
            return BitConverter.ToString(hash).Replace("-", ":").ToLower();
        }
        catch { return "неизвестно"; }
    }

    public bool IsValidPublicKey(string pem)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            _ = rsa.ExportRSAPublicKey();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
