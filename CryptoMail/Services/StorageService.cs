using System.Text.Json;
using CryptoMail.Models;
using System.IO;

namespace CryptoMail.Services;

public sealed class StorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string EnsureOutputDir(string? appBaseDir = null)
    {
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoMail");
        var outputDir = Path.Combine(appData, "Output");
        Directory.CreateDirectory(outputDir);
        return outputDir;
    }

    public string SaveEnvelope(string? appBaseDir, Envelope envelope)
    {
        var outputDir = EnsureOutputDir();
        var path = Path.Combine(outputDir, $"received_envelope_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(envelope, JsonOptions));
        return path;
    }

    public Envelope ReadEnvelopeFromJson(string json)
        => JsonSerializer.Deserialize<Envelope>(json) ?? throw new InvalidDataException("Envelope JSON is invalid.");

    public string SaveDecryptedFile(string? appBaseDir, string originalName, byte[] fileBytes)
    {
        var outputDir = EnsureOutputDir();
        var safeName = string.IsNullOrWhiteSpace(originalName) ? "decrypted.bin" : originalName;
        var finalPath = Path.Combine(outputDir, $"decrypted_{DateTime.Now:yyyyMMdd_HHmmss}_{safeName}");
        File.WriteAllBytes(finalPath, fileBytes);
        return finalPath;
    }
}
