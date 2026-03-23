using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using CryptoMail.Models;
using CryptoMail.Services;

namespace CryptoMail.ViewModels;

public sealed class ReceiverViewModel : BaseViewModel
{
    private const int ReceiveTimeoutSeconds = 20;

    private readonly KeyService _keyService;
    private readonly CryptoService _cryptoService;
    private readonly PackageService _packageService;
    private readonly EmailService _emailService;
    private readonly StorageService _storageService;
    private readonly LogViewModel _log;

    private string _statusText = "Готов";
    private bool _isBusy;
    private CancellationTokenSource? _receiveCancellationSource;

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
        CancelReceiveCommand = new RelayCommand(_ => CancelReceive(), _ => IsBusy);
    }

    public EmailSettings Settings { get; } = new();
    public KeyPaths KeyPaths { get; }
    public RelayCommand ReceiveAndDecryptCommand { get; }
    public RelayCommand CancelReceiveCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ReceiveAndDecryptCommand.RaiseCanExecuteChanged();
                CancelReceiveCommand.RaiseCanExecuteChanged();
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
        CancellationTokenSource? userCancel = null;
        CancellationTokenSource? timeoutCancel = null;
        CancellationTokenSource? linkedCancellation = null;

        try
        {
            IsBusy = true;
            StatusText = "Получение...";
            _log.Add("Подключение к IMAP-серверу...");

            userCancel = new CancellationTokenSource();
            timeoutCancel = new CancellationTokenSource(TimeSpan.FromSeconds(ReceiveTimeoutSeconds));
            linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(userCancel.Token, timeoutCancel.Token);
            _receiveCancellationSource = userCancel;

            if (!_keyService.RecipientPrivateKeyExists(KeyPaths))
            {
                throw new FileNotFoundException("Приватный ключ получателя не найден. Сначала создайте ключи получателя на вкладке 'КЛЮЧИ'.");
            }

            var recipientPrivatePem = _keyService.LoadPem(KeyPaths.RecipientPrivatePath);
            var subjectFilter = string.IsNullOrWhiteSpace(Settings.SubjectFilter)
                ? "CryptoMail"
                : Settings.SubjectFilter.Trim();

            if (!string.Equals(subjectFilter, Settings.SubjectFilter, StringComparison.Ordinal))
            {
                _log.Add($"Фильтр темы был пуст или содержал лишние пробелы. Использую: {subjectFilter}");
            }

            string currentRecipientFingerprint = File.Exists(KeyPaths.RecipientPublicPath)
                ? _keyService.GetFingerprint(_keyService.LoadPem(KeyPaths.RecipientPublicPath))
                : "неизвестно";

            _log.Add($"Текущий отпечаток ключа получателя: {currentRecipientFingerprint}");

            var candidates = await _emailService.DownloadMatchingAttachmentsAsync(
                settings: Settings,
                subjectFilter: subjectFilter,
                cancellationToken: linkedCancellation.Token,
                progress: message => _log.Add(message));

            if (candidates.Count == 0)
            {
                throw new InvalidOperationException("Не найдено подходящих сообщений с защищенным вложением.");
            }

            _log.Add($"Найдено подходящих вложений: {candidates.Count}. Проверка от нового к старому...");

            byte[]? zipBytes = null;
            string? decryptFailureHint = null;

            foreach (var candidate in candidates)
            {
                linkedCancellation.Token.ThrowIfCancellationRequested();
                _log.Add($"Проверка вложения: {candidate.AttachmentName} | тема: {candidate.Subject}");

                try
                {
                    var envelope = JsonSerializer.Deserialize<Envelope>(candidate.Content)
                        ?? throw new InvalidOperationException("Ошибка формата полученного пакета.");

                    zipBytes = _cryptoService.Decrypt(envelope, recipientPrivatePem);
                    _log.Add("Пакет получен. Расшифровка (AES-GCM + RSA-OAEP)...");
                    break;
                }
                catch (JsonException)
                {
                    _log.Add("Пропуск: вложение не является корректным CryptoMail envelope.");
                }
                catch (AuthenticationTagMismatchException)
                {
                    decryptFailureHint = "Найдено письмо, но пакет поврежден или был изменен после отправки.";
                    _log.Add("Пропуск: не совпал тег целостности AES-GCM.");
                }
                catch (CryptographicException ex)
                {
                    decryptFailureHint = IsWrongRecipientKey(ex)
                        ? $"Письмо зашифровано не для вашего текущего ключа получателя ({currentRecipientFingerprint}). Обычно это значит, что отправитель использовал другой recipient_public.pem или ключи получателя были пересозданы после отправки."
                        : $"Криптографическая ошибка при расшифровке: {ex.Message}";

                    _log.Add($"Пропуск: {decryptFailureHint}");
                }
            }

            if (zipBytes is null)
            {
                throw new InvalidOperationException(decryptFailureHint ?? "Не удалось расшифровать ни одно подходящее письмо.");
            }

            _log.Add("Распаковка и проверка цифровой подписи (RSA-PSS)...");
            var (fileBytes, signature, meta, senderPublicPem) = _packageService.ReadZip(zipBytes);

            string actualFingerprint = _keyService.GetFingerprint(senderPublicPem);
            _log.Add($"Получен отпечаток отправителя: {actualFingerprint}");

            bool isTrusted = false;
            string trustedPath = KeyPaths.TrustedSenderPublicPath;
            if (File.Exists(trustedPath))
            {
                string trustedPem = File.ReadAllText(trustedPath);
                string trustedFingerprint = _keyService.GetFingerprint(trustedPem);

                if (trustedFingerprint == actualFingerprint)
                {
                    isTrusted = true;
                    _log.Add("Ключ отправителя подтвержден: он находится в списке доверенных.");
                }
                else
                {
                    _log.Add($"ВНИМАНИЕ: отпечаток ключа отправителя не совпадает с доверенным. Доверенный: {trustedFingerprint}");
                }
            }
            else
            {
                _log.Add("Предупреждение: список доверенных отправителей пуст. Подпись будет проверена без подтверждения личности.");
            }

            var isValid = _cryptoService.Verify(fileBytes, signature, senderPublicPem);

            if (isValid)
            {
                var savedPath = _storageService.SaveDecryptedFile(null, meta.FileName, fileBytes);
                StatusText = "Успешно расшифровано!";
                _log.Add($"ПОДПИСЬ ВЕРНА. Файл сохранен в: {Path.GetDirectoryName(savedPath)}");

                if (!isTrusted && File.Exists(trustedPath))
                {
                    _log.Add("ПРЕДУПРЕЖДЕНИЕ: подпись верна, но отправитель не является доверенным.");
                }
            }
            else
            {
                StatusText = "Ошибка проверки подписи!";
                _log.Add("ВНИМАНИЕ: цифровая подпись неверна. Файл может быть подделан.");
            }
        }
        catch (OperationCanceledException) when (timeoutCancel?.IsCancellationRequested == true)
        {
            StatusText = "Время ожидания истекло";
            _log.Add($"Операция прервана по таймауту ({ReceiveTimeoutSeconds} сек.). Попробуйте уточнить тему письма или повторить попытку позже.");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Получение отменено";
            _log.Add("Операция получения отменена пользователем.");
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка получения";
            _log.Add($"КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
        }
        finally
        {
            linkedCancellation?.Dispose();
            timeoutCancel?.Dispose();
            userCancel?.Dispose();
            _receiveCancellationSource = null;
            IsBusy = false;
        }
    }

    private void CancelReceive()
    {
        if (!IsBusy)
        {
            return;
        }

        StatusText = "Отмена...";
        _log.Add("Запрошена отмена получения.");
        _receiveCancellationSource?.Cancel();
    }

    private static bool IsWrongRecipientKey(CryptographicException ex)
        => (uint)ex.HResult == 0xc100000d;
}
