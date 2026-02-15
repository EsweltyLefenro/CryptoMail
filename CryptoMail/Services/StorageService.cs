using System.Text.Json;
using CryptoMail.Models;
using System.IO;

namespace CryptoMail.Services;

public sealed class StorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string EnsureOutputDir(string appBaseDir)
    {
        var outputDir = Path.Combine(appBaseDir, "Output");
        Directory.CreateDirectory(outputDir);
        return outputDir;
    }

    public string SaveEnvelope(string appBaseDir, Envelope envelope)
    {
        var outputDir = EnsureOutputDir(appBaseDir);
        var path = Path.Combine(outputDir, "received_envelope.json");
        File.WriteAllText(path, JsonSerializer.Serialize(envelope, JsonOptions));
        return path;
    }

    public Envelope ReadEnvelopeFromJson(string json)
        => JsonSerializer.Deserialize<Envelope>(json) ?? throw new InvalidDataException("Envelope JSON is invalid.");

    public string SaveDecryptedFile(string appBaseDir, string originalName, byte[] fileBytes)
    {
        var outputDir = EnsureOutputDir(appBaseDir);
        var safeName = string.IsNullOrWhiteSpace(originalName) ? "decrypted.bin" : originalName;
        var finalPath = Path.Combine(outputDir, $"decrypted_{DateTime.Now:yyyyMMdd_HHmmss}_{safeName}");
        File.WriteAllBytes(finalPath, fileBytes);
        return finalPath;
    }
}
