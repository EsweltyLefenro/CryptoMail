using System.IO.Compression;
using System.IO;
using System.Text.Json;
using System.Text;
using CryptoMail.Models;

namespace CryptoMail.Services;

public sealed class PackageService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public byte[] BuildZip(byte[] fileBytes, byte[] signatureBytes, PackageMeta meta, string senderPublicPem)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "payload.bin", fileBytes);
            WriteEntry(archive, "signature.bin", signatureBytes);
            WriteEntry(archive, "meta.json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(meta, JsonOptions)));
            WriteEntry(archive, "sender_public.pem", Encoding.UTF8.GetBytes(senderPublicPem));
        }

        return ms.ToArray();
    }

    public (byte[] FileBytes, byte[] SignatureBytes, PackageMeta Meta, string SenderPublicPem) ReadZip(byte[] zipBytes)
    {
        using var ms = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        var fileBytes = ReadEntry(archive, "payload.bin");
        var signatureBytes = ReadEntry(archive, "signature.bin");
        var metaBytes = ReadEntry(archive, "meta.json");
        var senderPublicBytes = ReadEntry(archive, "sender_public.pem");

        var meta = JsonSerializer.Deserialize<PackageMeta>(metaBytes) ?? new PackageMeta();
        var senderPublicPem = Encoding.UTF8.GetString(senderPublicBytes);

        return (fileBytes, signatureBytes, meta, senderPublicPem);
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] data)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.SmallestSize);
        using var stream = entry.Open();
        stream.Write(data, 0, data.Length);
    }

    private static byte[] ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"Entry '{name}' not found in package.");
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
