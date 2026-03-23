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
    private string _statusText = "Готов";

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

        KeyPaths = _keyService.GetDefaultKeyPaths();

        ChooseFileCommand = new RelayCommand(_ => ChooseFile());
        SendCommand = new RelayCommand(async _ => await SendAsync(), _ => !IsBusy);
        GenerateKeysCommand = new RelayCommand(_ => GenerateAllKeys());

        Subject = "CryptoMail";
    }

    public RelayCommand ChooseFileCommand { get; }
    public RelayCommand SendCommand { get; }
    public RelayCommand GenerateKeysCommand { get; }

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
            _log.Add($"Выбран файл: {Path.GetFileName(path)}");
        }
    }

    private void GenerateAllKeys()
    {
        try
        {
            _keyService.GenerateSenderKeys(KeyPaths);
            _keyService.GenerateRecipientKeys(KeyPaths);
            var senderPublicPem = _keyService.LoadPem(KeyPaths.SenderPublicPath);
            File.WriteAllText(KeyPaths.TrustedSenderPublicPath, senderPublicPem);
            _log.Add("Все наборы ключей успешно пересозданы. trusted_sender.pem обновлен автоматически.");
        }
        catch (Exception ex) { _log.Add($"Ошибка генерации: {ex.Message}"); }
    }

    private async Task SendAsync()
    {
        try
        {
            IsBusy = true;
            StatusText = "Отправка...";

            if (string.IsNullOrWhiteSpace(SelectedFilePath) || !File.Exists(SelectedFilePath))
            {
                throw new FileNotFoundException("Сначала выберите файл.");
            }

            if (!_keyService.SenderKeysExist(KeyPaths))
            {
                throw new FileNotFoundException("Ключи отправителя не найдены. Создайте их на вкладке 'КЛЮЧИ'.");
            }

            var senderPrivatePem = _keyService.LoadPem(KeyPaths.SenderPrivatePath);
            var senderPublicPem = _keyService.LoadPem(KeyPaths.SenderPublicPath);
            var (recipientPublicPem, recipientKeyDescription) = ResolveRecipientPublicKey();

            var fileBytes = await File.ReadAllBytesAsync(SelectedFilePath);
            
            _log.Add("Подпись файла (RSA-PSS)...");
            var signature = _cryptoService.Sign(fileBytes, senderPrivatePem);
            
            var meta = new PackageMeta
            {
                FileName = Path.GetFileName(SelectedFilePath),
                CreatedUtc = DateTime.UtcNow,
                SenderEmail = Settings.Login
            };

            _log.Add($"Используется ключ получателя: {recipientKeyDescription}");
            _log.Add("Упаковка и шифрование (AES-GCM)...");
            var zip = _packageService.BuildZip(fileBytes, signature, meta, senderPublicPem);
            var envelope = _cryptoService.Encrypt(zip, recipientPublicPem);
            var envelopeJson = JsonSerializer.Serialize(envelope);

            Settings.FromAddress = Settings.Login;
            await _emailService.SendAsync(Settings, RecipientEmail, Subject, "envelope.json", envelopeJson);

            StatusText = "Успешно отправлено";
            _log.Add("Письмо успешно отправлено.");
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка отправки";
            _log.Add($"Ошибка: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private (string Pem, string Description) ResolveRecipientPublicKey()
    {
        if (string.IsNullOrWhiteSpace(RecipientEmail))
            throw new ArgumentException("Email получателя обязателен.");

        bool isSelfSend = !string.IsNullOrWhiteSpace(Settings.Login) &&
                          RecipientEmail.Equals(Settings.Login, StringComparison.OrdinalIgnoreCase);

        if (isSelfSend)
        {
            if (!_keyService.PublicKeyExists(KeyPaths.RecipientPublicPath))
                throw new FileNotFoundException("Локальный ключ получателя не найден. Создайте его на вкладке 'КЛЮЧИ'.");

            return (_keyService.LoadPem(KeyPaths.RecipientPublicPath), "локальный recipient_public.pem (отправка самому себе)");
        }

        if (_keyService.PublicKeyExists(KeyPaths.PartnerRecipientPublicPath))
        {
            return (_keyService.LoadPem(KeyPaths.PartnerRecipientPublicPath), "загруженный ключ другого получателя");
        }

        throw new FileNotFoundException("Для отправки другому человеку сначала добавьте его recipient_public.pem на вкладке 'КЛЮЧИ'.");
    }
}
