using System.Text.Json;
using CryptoMail.Models;
using CryptoMail.Services;
using System.IO;

namespace CryptoMail.ViewModels;

public sealed class ReceiverViewModel : BaseViewModel
{
    private readonly KeyService _keyService;
    private readonly CryptoService _cryptoService;
    private readonly PackageService _packageService;
    private readonly EmailService _emailService;
    private readonly StorageService _storageService;
    private readonly LogViewModel _log;

    private string _statusText = "Готов";
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

        KeyPaths = _keyService.GetDefaultKeyPaths();
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
            StatusText = "Получение...";
            _log.Add("Подключение к IMAP-серверу...");

            if (!_keyService.AllKeysExist(KeyPaths))
            {
                throw new FileNotFoundException("Ключи не найдены. Сначала создайте их на вкладке 'КЛЮЧИ'.");
            }

            var envelopeJson = await _emailService.DownloadLatestAttachmentAsync(Settings, Settings.SubjectFilter);
            
            _log.Add("Пакет получен. Расшифровка (AES-GCM + RSA-OAEP)...");
            var envelope = JsonSerializer.Deserialize<Envelope>(envelopeJson) ?? throw new Exception("Ошибка формата полученного пакета.");
            
            var recipientPrivatePem = _keyService.LoadPem(KeyPaths.RecipientPrivatePath);
            var zipBytes = _cryptoService.Decrypt(envelope, recipientPrivatePem);

            _log.Add("Распаковка и проверка цифровой подписи (RSA-PSS)...");
            var (fileBytes, signature, meta, senderPublicPem) = _packageService.ReadZip(zipBytes);
            
            string actualFingerprint = _keyService.GetFingerprint(senderPublicPem);
            _log.Add($"Получен отпечаток отправителя: {actualFingerprint}");

            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoMail");
            string trustedPath = Path.Combine(appData, "TrustedSenders", "trusted_sender.pem");
            
            bool isTrusted = false;
            if (File.Exists(trustedPath))
            {
                string trustedPem = File.ReadAllText(trustedPath);
                string trustedFingerprint = _keyService.GetFingerprint(trustedPem);
                
                if (trustedFingerprint == actualFingerprint)
                {
                    isTrusted = true;
                    _log.Add("Ключ отправителя подтвержден (находится в списке доверенных).");
                }
                else
                {
                    _log.Add("ВНИМАНИЕ: Отпечаток ключа отправителя НЕ СОВПАДАЕТ с доверенным!");
                }
            }
            else
            {
                _log.Add("Предупреждение: Список доверенных отправителей пуст. Проверка подписи будет выполнена без подтверждения личности.");
            }

            var isValid = _cryptoService.Verify(fileBytes, signature, senderPublicPem);

            if (isValid)
            {
                var savedPath = _storageService.SaveDecryptedFile(null, meta.FileName, fileBytes);
                StatusText = "Успешно расшифровано!";
                _log.Add($"ПОДПИСЬ ВЕРНА. Файл сохранен в: {Path.GetDirectoryName(savedPath)}");
                
                if (!isTrusted && File.Exists(trustedPath))
                    _log.Add("ПРЕДУПРЕЖДЕНИЕ: Подпись верна, но отправитель не является доверенным.");
            }
            else
            {
                StatusText = "Ошибка проверки подписи!";
                _log.Add("ВНИМАНИЕ: ЦИФРОВАЯ ПОДПИСЬ НЕВЕРНА! Файл может быть подделан.");
            }
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка получения";
            _log.Add($"КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
