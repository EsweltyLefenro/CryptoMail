using System.Text.Json;
using CryptoMail.Models;
using CryptoMail.Services;
using System.IO;

namespace CryptoMail.ViewModels;

public sealed class SenderViewModel : BaseViewModel
{
    private readonly KeyService _keyService;
    private readonly CryptoService _cryptoService;
    private readonly PackageService _packageService;
    private readonly EmailService _emailService;
    private readonly FileDialogService _fileDialogService;
    private readonly StorageService _storageService;
    private readonly LogViewModel _log;

    private string _selectedFilePath = string.Empty;
    private bool _isBusy;
    private string _statusText = "Ready";

    public SenderViewModel(
        KeyService keyService,
        CryptoService cryptoService,
        PackageService packageService,
        EmailService emailService,
        FileDialogService fileDialogService,
        StorageService storageService,
        LogViewModel log)
    {
        _keyService = keyService;
        _cryptoService = cryptoService;
        _packageService = packageService;
        _emailService = emailService;
        _fileDialogService = fileDialogService;
        _storageService = storageService;
        _log = log;

        var baseDir = AppContext.BaseDirectory;
        KeyPaths = _keyService.GetDefaultKeyPaths(baseDir);

        ChooseFileCommand = new RelayCommand(_ => ChooseFile());
        GenerateKeysCommand = new RelayCommand(_ => GenerateKeys());
        SendCommand = new RelayCommand(async _ => await SendAsync(), _ => !IsBusy);

        Subject = "CryptoMail Package";
    }

    public RelayCommand ChooseFileCommand { get; }
    public RelayCommand GenerateKeysCommand { get; }
    public RelayCommand SendCommand { get; }

    public EmailSettings Settings { get; } = new();

    public KeyPaths KeyPaths { get; }

    public string RecipientEmail { get; set; } = string.Empty;

    public string Subject { get; set; }

    public string SelectedFilePath
    {
        get => _selectedFilePath;
        set => SetProperty(ref _selectedFilePath, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private void ChooseFile()
    {
        var path = _fileDialogService.OpenFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            SelectedFilePath = path;
            _log.Add($"Selected file: {path}");
        }
    }

    private void GenerateKeys()
    {
        _keyService.GenerateSenderKeys(KeyPaths);
        _keyService.GenerateRecipientKeys(KeyPaths);
        StatusText = "Keys generated.";
        _log.Add("Sender and recipient keys generated in Keys/.");
    }

    private async Task SendAsync()
    {
        try
        {
            IsBusy = true;

            if (string.IsNullOrWhiteSpace(SelectedFilePath) || !File.Exists(SelectedFilePath))
            {
                throw new FileNotFoundException("Select a valid file first.");
            }

            if (!_keyService.AllKeysExist(KeyPaths))
            {
                throw new FileNotFoundException("Keys not found. Generate keys first.");
            }

            var senderPrivatePem = _keyService.LoadPem(KeyPaths.SenderPrivatePath);
            var senderPublicPem = _keyService.LoadPem(KeyPaths.SenderPublicPath);
            var recipientPublicPem = _keyService.LoadPem(KeyPaths.RecipientPublicPath);

            var fileBytes = await File.ReadAllBytesAsync(SelectedFilePath);
            var signature = _cryptoService.Sign(fileBytes, senderPrivatePem);
            var meta = new PackageMeta
            {
                FileName = Path.GetFileName(SelectedFilePath),
                CreatedUtc = DateTime.UtcNow,
                SenderEmail = Settings.FromAddress
            };

            var zip = _packageService.BuildZip(fileBytes, signature, meta, senderPublicPem);
            var envelope = _cryptoService.Encrypt(zip, recipientPublicPem);
            var envelopeJson = JsonSerializer.Serialize(envelope);

            var localPath = _storageService.SaveEnvelope(AppContext.BaseDirectory, envelope);
            _log.Add($"Envelope saved locally: {localPath}");

            await _emailService.SendAsync(Settings, RecipientEmail, Subject, "envelope.json", envelopeJson);

            StatusText = "Sent successfully.";
            _log.Add("Email sent successfully.");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            _log.Add(StatusText);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
