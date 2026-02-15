namespace CryptoMail.Models;

public sealed class Envelope
{
    public string EncKeyB64 { get; set; } = string.Empty;
    public string NonceB64 { get; set; } = string.Empty;
    public string TagB64 { get; set; } = string.Empty;
    public string DataB64 { get; set; } = string.Empty;
}
