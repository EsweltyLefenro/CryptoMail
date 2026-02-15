using CryptoMail.Models;
using CryptoMail.Services;

namespace CryptoMail.ViewModels;

public sealed class ReceiverViewModel : BaseViewModel
{
    private readonly KeyService _keyService;
    private readonly CryptoService _cryptoService;
    private readonly PackageService _packageService;
    private readonly EmailService _emailService;
    private readonly StorageService _storageService;
    private readonly LogViewModel _log;

    private string _statusText = "Ready";
    private bool _isBusy;

    public ReceiverViewModel(
        KeyService keyService,
        CryptoService cryptoService,
        PackageService packageService,
        EmailService emailService,
        StorageService storageService,
        LogViewModel log)
    {
        _keyService = keyService;
        _cryptoService = cryptoService;
        _packageService = packageService;
        _emailService = emailService;
        _storageService = storageService;
        _log = log;

        var baseDir = AppContext.BaseDirectory;
        KeyPaths = _keyService.GetDefaultKeyPaths(baseDir);

        ReceiveAndDecryptCommand = new RelayCommand(async _ => await ReceiveAndDecryptAsync(), _ => !IsBusy);
    }

    public EmailSettings Settings { get; } = new();

    public KeyPaths KeyPaths { get; }

    public RelayCommand ReceiveAndDecryptCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ReceiveAndDecryptCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private async Task ReceiveAndDecryptAsync()
    {
        try
        {
            IsBusy = true;

            var envelopeJson = await _emailService.DownloadLatestAttachmentAsync(Settings, Settings.SubjectFilter);
            var envelope = _storageService.ReadEnvelopeFromJson(envelopeJson);
            _storageService.SaveEnvelope(AppContext.BaseDirectory, envelope);

            var recipientPrivatePem = _keyService.LoadPem(KeyPaths.RecipientPrivatePath);
            var zipBytes = _cryptoService.Decrypt(envelope, recipientPrivatePem);

            var (fileBytes, signature, meta, senderPublicPem) = _packageService.ReadZip(zipBytes);
            var isValid = _cryptoService.Verify(fileBytes, signature, senderPublicPem);

            var savedPath = _storageService.SaveDecryptedFile(AppContext.BaseDirectory, meta.FileName, fileBytes);

            StatusText = $"Signature valid: {isValid}. Saved: {savedPath}";
            _log.Add(StatusText);
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
