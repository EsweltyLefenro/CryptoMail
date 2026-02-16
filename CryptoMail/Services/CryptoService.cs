using System.Security.Cryptography;
using CryptoMail.Models;

namespace CryptoMail.Services;

public sealed class CryptoService
{
    public byte[] Sign(byte[] fileBytes, string senderPrivatePem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(senderPrivatePem);
        return rsa.SignData(fileBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
    }

    public bool Verify(byte[] fileBytes, byte[] signatureBytes, string senderPublicPem)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(senderPublicPem);
            return rsa.VerifyData(fileBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch { return false; }
    }

    public Envelope Encrypt(byte[] zipBytes, string recipientPublicPem)
    {
        var aesKey = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[zipBytes.Length];

        using (var aesGcm = new AesGcm(aesKey, tag.Length))
        {
            aesGcm.Encrypt(nonce, zipBytes, cipher, tag);
        }

        byte[] encryptedKey;
        using (var rsa = RSA.Create())
        {
            rsa.ImportFromPem(recipientPublicPem);
            encryptedKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
        }

        return new Envelope
        {
            EncKeyB64 = Convert.ToBase64String(encryptedKey),
            NonceB64 = Convert.ToBase64String(nonce),
            TagB64 = Convert.ToBase64String(tag),
            DataB64 = Convert.ToBase64String(cipher)
        };
    }

    public byte[] Decrypt(Envelope envelope, string recipientPrivatePem)
    {
        var encKey = Convert.FromBase64String(envelope.EncKeyB64);
        var nonce = Convert.FromBase64String(envelope.NonceB64);
        var tag = Convert.FromBase64String(envelope.TagB64);
        var data = Convert.FromBase64String(envelope.DataB64);

        byte[] aesKey;
        using (var rsa = RSA.Create())
        {
            rsa.ImportFromPem(recipientPrivatePem);
            aesKey = rsa.Decrypt(encKey, RSAEncryptionPadding.OaepSHA256);
        }

        var plain = new byte[data.Length];
        using (var aesGcm = new AesGcm(aesKey, tag.Length))
        {
            aesGcm.Decrypt(nonce, data, tag, plain);
        }

        return plain;
    }
}
