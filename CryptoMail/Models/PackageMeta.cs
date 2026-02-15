namespace CryptoMail.Models;

public sealed class PackageMeta
{
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string SenderEmail { get; set; } = string.Empty;
}
