namespace CryptoMail.Models;

public sealed class KeyPaths
{
    public string SenderPrivatePath { get; set; } = string.Empty;
    public string SenderPublicPath { get; set; } = string.Empty;
    public string RecipientPrivatePath { get; set; } = string.Empty;
    public string RecipientPublicPath { get; set; } = string.Empty;
    public string PartnerRecipientPublicPath { get; set; } = string.Empty;
    public string TrustedSenderPublicPath { get; set; } = string.Empty;
}
