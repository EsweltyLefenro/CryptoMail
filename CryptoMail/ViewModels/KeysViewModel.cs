using System.Diagnostics;
using System.IO;
using System.Windows;
using CryptoMail.Models;
using CryptoMail.Services;

namespace CryptoMail.ViewModels;

public sealed class KeysViewModel : BaseViewModel
{
    private readonly KeyService _keyService;
    private readonly LogViewModel _log;

    private string _senderFingerprint = "неизвестно";
    private string _recipientFingerprint = "неизвестно";
    private string _trustedKeyPath = string.Empty;
    private string _trustedFingerprint = string.Empty;
    private string _partnerRecipientKeyPath = string.Empty;
    private string _partnerRecipientFingerprint = string.Empty;

    public KeysViewModel(KeyService keyService, LogViewModel log)
    {
        _keyService = keyService;
        _log = log;
        KeyPaths = _keyService.GetDefaultKeyPaths();

        GenerateSenderKeysCommand = new RelayCommand(_ => GenerateSenderKeys());
        GenerateRecipientKeysCommand = new RelayCommand(_ => GenerateRecipientKeys());
        OpenKeysFolderCommand = new RelayCommand(_ => OpenKeysFolder());
        OpenOutputFolderCommand = new RelayCommand(_ => OpenOutputFolder());
        AddTrustedKeyCommand = new RelayCommand(_ => AddTrustedKey());
        AddPartnerRecipientKeyCommand = new RelayCommand(_ => AddPartnerRecipientKey());
        ClearPartnerRecipientKeyCommand = new RelayCommand(_ => ClearPartnerRecipientKey(), _ => HasPartnerRecipientKey);

        OpenRecipientPublicFileCommand = new RelayCommand(_ => OpenFileInExplorer(KeyPaths.RecipientPublicPath), _ => HasRecipientPublicKey);
        OpenSenderPublicFileCommand = new RelayCommand(_ => OpenFileInExplorer(KeyPaths.SenderPublicPath), _ => HasSenderPublicKey);
        OpenTrustedKeyFileCommand = new RelayCommand(_ => OpenFileInExplorer(KeyPaths.TrustedSenderPublicPath), _ => HasTrustedKey);
        OpenPartnerRecipientKeyFileCommand = new RelayCommand(_ => OpenFileInExplorer(KeyPaths.PartnerRecipientPublicPath), _ => HasPartnerRecipientKey);

        UpdateFingerprints();
        LoadTrustedKeyInfo();
        LoadPartnerRecipientKeyInfo();
        RefreshCommandStates();
    }

    public KeyPaths KeyPaths { get; }
    public RelayCommand GenerateSenderKeysCommand { get; }
    public RelayCommand GenerateRecipientKeysCommand { get; }
    public RelayCommand OpenKeysFolderCommand { get; }
    public RelayCommand OpenOutputFolderCommand { get; }
    public RelayCommand AddTrustedKeyCommand { get; }
    public RelayCommand AddPartnerRecipientKeyCommand { get; }
    public RelayCommand ClearPartnerRecipientKeyCommand { get; }
    public RelayCommand OpenRecipientPublicFileCommand { get; }
    public RelayCommand OpenSenderPublicFileCommand { get; }
    public RelayCommand OpenTrustedKeyFileCommand { get; }
    public RelayCommand OpenPartnerRecipientKeyFileCommand { get; }

    public string SenderFingerprint
    {
        get => _senderFingerprint;
        set => SetProperty(ref _senderFingerprint, value);
    }

    public string RecipientFingerprint
    {
        get => _recipientFingerprint;
        set => SetProperty(ref _recipientFingerprint, value);
    }

    public string TrustedKeyPath
    {
        get => _trustedKeyPath;
        set => SetProperty(ref _trustedKeyPath, value);
    }

    public string TrustedFingerprint
    {
        get => _trustedFingerprint;
        set
        {
            if (SetProperty(ref _trustedFingerprint, value))
            {
                OnPropertyChanged(nameof(HasTrustedKey));
                RefreshCommandStates();
            }
        }
    }

    public string PartnerRecipientKeyPath
    {
        get => _partnerRecipientKeyPath;
        set => SetProperty(ref _partnerRecipientKeyPath, value);
    }

    public string PartnerRecipientFingerprint
    {
        get => _partnerRecipientFingerprint;
        set
        {
            if (SetProperty(ref _partnerRecipientFingerprint, value))
            {
                OnPropertyChanged(nameof(HasPartnerRecipientKey));
                RefreshCommandStates();
            }
        }
    }

    public bool HasTrustedKey => !string.IsNullOrWhiteSpace(TrustedFingerprint);
    public bool HasPartnerRecipientKey => !string.IsNullOrWhiteSpace(PartnerRecipientFingerprint);
    public bool HasRecipientPublicKey => File.Exists(KeyPaths.RecipientPublicPath);
    public bool HasSenderPublicKey => File.Exists(KeyPaths.SenderPublicPath);

    public string RecipientPublicFileName => Path.GetFileName(KeyPaths.RecipientPublicPath);
    public string SenderPublicFileName => Path.GetFileName(KeyPaths.SenderPublicPath);
    public string TrustedSenderFileName => Path.GetFileName(KeyPaths.TrustedSenderPublicPath);
    public string PartnerRecipientFileName => Path.GetFileName(KeyPaths.PartnerRecipientPublicPath);

    private void GenerateSenderKeys()
    {
        try
        {
            _keyService.GenerateSenderKeys(KeyPaths);
            SyncTrustedSenderWithLocalSender();
            UpdateFingerprints();
            _log.Add("Ключи отправителя успешно созданы. trusted_sender.pem обновлен автоматически.");
        }
        catch (Exception ex)
        {
            _log.Add($"Ошибка: {ex.Message}");
        }
    }

    private void GenerateRecipientKeys()
    {
        try
        {
            _keyService.GenerateRecipientKeys(KeyPaths);
            UpdateFingerprints();
            _log.Add("Ключи получателя успешно созданы.");
        }
        catch (Exception ex)
        {
            _log.Add($"Ошибка: {ex.Message}");
        }
    }

    private void OpenKeysFolder()
    {
        OpenFolder(Path.GetDirectoryName(KeyPaths.SenderPublicPath) ?? string.Empty);
    }

    private void OpenOutputFolder()
    {
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoMail");
        string path = Path.Combine(appData, "Output");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        OpenFolder(path);
    }

    private void AddTrustedKey()
    {
        try
        {
            if (!TrySelectPublicKey("Выберите sender_public.pem отправителя", out var pem))
            {
                return;
            }

            string fingerprint = _keyService.GetFingerprint(pem);
            if (fingerprint == RecipientFingerprint)
            {
                MessageBox.Show(
                    "Похоже, вы выбрали recipient_public.pem. Для доверенного отправителя нужен sender_public.pem.",
                    "Проверьте выбранный ключ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            SaveTrustedSenderPem(pem);
            _log.Add("Доверенный ключ отправителя успешно добавлен.");
        }
        catch (Exception ex)
        {
            _log.Add($"Ошибка при добавлении ключа: {ex.Message}");
        }
    }

    private void AddPartnerRecipientKey()
    {
        try
        {
            if (!TrySelectPublicKey("Выберите recipient_public.pem получателя", out var pem))
            {
                return;
            }

            SavePartnerRecipientPem(pem);
            _log.Add("Публичный ключ получателя для отправки успешно добавлен.");
        }
        catch (Exception ex)
        {
            _log.Add($"Ошибка при добавлении ключа получателя: {ex.Message}");
        }
    }

    private void ClearPartnerRecipientKey()
    {
        try
        {
            if (File.Exists(KeyPaths.PartnerRecipientPublicPath))
            {
                File.Delete(KeyPaths.PartnerRecipientPublicPath);
            }

            PartnerRecipientKeyPath = string.Empty;
            PartnerRecipientFingerprint = string.Empty;
            _log.Add("Публичный ключ другого получателя удален. Для отправки себе будет использоваться локальный ключ.");
        }
        catch (Exception ex)
        {
            _log.Add($"Ошибка при удалении ключа получателя: {ex.Message}");
        }
    }

    private void LoadTrustedKeyInfo()
    {
        string path = KeyPaths.TrustedSenderPublicPath;
        if (File.Exists(path))
        {
            TrustedKeyPath = path;
            TrustedFingerprint = _keyService.GetFingerprint(File.ReadAllText(path));
        }
        else if (File.Exists(KeyPaths.SenderPublicPath))
        {
            SyncTrustedSenderWithLocalSender();
        }
    }

    private void LoadPartnerRecipientKeyInfo()
    {
        string path = KeyPaths.PartnerRecipientPublicPath;
        if (File.Exists(path))
        {
            string pem = File.ReadAllText(path);
            PartnerRecipientKeyPath = path;
            PartnerRecipientFingerprint = _keyService.GetFingerprint(pem);
        }
    }

    private void UpdateFingerprints()
    {
        SenderFingerprint = HasSenderPublicKey
            ? _keyService.GetFingerprint(_keyService.LoadPem(KeyPaths.SenderPublicPath))
            : "не создан";

        RecipientFingerprint = HasRecipientPublicKey
            ? _keyService.GetFingerprint(_keyService.LoadPem(KeyPaths.RecipientPublicPath))
            : "не создан";

        OnPropertyChanged(nameof(HasSenderPublicKey));
        OnPropertyChanged(nameof(HasRecipientPublicKey));
        RefreshCommandStates();
    }

    private bool TrySelectPublicKey(string title, out string pem)
    {
        pem = string.Empty;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PEM files (*.pem)|*.pem|All files (*.*)|*.*",
            Title = title
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        pem = File.ReadAllText(dialog.FileName);
        if (!_keyService.IsValidPublicKey(pem))
        {
            MessageBox.Show(
                "Выбранный файл не является корректным публичным ключом RSA.",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            pem = string.Empty;
            return false;
        }

        return true;
    }

    private void SavePartnerRecipientPem(string pem)
    {
        ValidatePublicKeyPem(pem, "получателя");
        File.WriteAllText(KeyPaths.PartnerRecipientPublicPath, pem);
        PartnerRecipientKeyPath = KeyPaths.PartnerRecipientPublicPath;
        PartnerRecipientFingerprint = _keyService.GetFingerprint(pem);

        if (PartnerRecipientFingerprint == SenderFingerprint)
        {
            MessageBox.Show(
                "Похоже, вы загрузили sender_public.pem. Для отправки другому человеку нужен его recipient_public.pem.",
                "Проверьте выбранный ключ",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void SaveTrustedSenderPem(string pem)
    {
        ValidatePublicKeyPem(pem, "отправителя");
        File.WriteAllText(KeyPaths.TrustedSenderPublicPath, pem);
        TrustedKeyPath = KeyPaths.TrustedSenderPublicPath;
        TrustedFingerprint = _keyService.GetFingerprint(pem);
    }

    private void SyncTrustedSenderWithLocalSender()
    {
        if (!File.Exists(KeyPaths.SenderPublicPath))
        {
            return;
        }

        SaveTrustedSenderPem(File.ReadAllText(KeyPaths.SenderPublicPath));
        OnPropertyChanged(nameof(TrustedSenderFileName));
    }

    private void ValidatePublicKeyPem(string pem, string role)
    {
        if (!_keyService.IsValidPublicKey(pem))
        {
            throw new InvalidOperationException($"Выбранный PEM не является корректным публичным ключом RSA {role}.");
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            _log.Add($"Ошибка: {ex.Message}");
        }
    }

    private void OpenFileInExplorer(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                _log.Add("Файл пока не создан.");
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log.Add($"Ошибка: {ex.Message}");
        }
    }

    private void RefreshCommandStates()
    {
        ClearPartnerRecipientKeyCommand.RaiseCanExecuteChanged();
        OpenRecipientPublicFileCommand.RaiseCanExecuteChanged();
        OpenSenderPublicFileCommand.RaiseCanExecuteChanged();
        OpenTrustedKeyFileCommand.RaiseCanExecuteChanged();
        OpenPartnerRecipientKeyFileCommand.RaiseCanExecuteChanged();
    }
}
