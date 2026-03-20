using CryptoMail.Models;
using CryptoMail.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CryptoMail.ViewModels;

public sealed class KeysViewModel : BaseViewModel
{
    private readonly KeyService _keyService;
    private readonly LogViewModel _log;
    private string _senderFingerprint = "неизвестно";
    private string _recipientFingerprint = "неизвестно";
    private string _trustedKeyPath = string.Empty;
    private string _trustedFingerprint = string.Empty;

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
        
        UpdateFingerprints();
        LoadTrustedKeyInfo();
    }

    public KeyPaths KeyPaths { get; }
    public RelayCommand GenerateSenderKeysCommand { get; }
    public RelayCommand GenerateRecipientKeysCommand { get; }
    public RelayCommand OpenKeysFolderCommand { get; }
    public RelayCommand OpenOutputFolderCommand { get; }
    public RelayCommand AddTrustedKeyCommand { get; }

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
            }
        }
    }

    public bool HasTrustedKey => !string.IsNullOrEmpty(TrustedFingerprint);

    private void GenerateSenderKeys()
    {
        try
        {
            _keyService.GenerateSenderKeys(KeyPaths);
            UpdateFingerprints();
            _log.Add("Ключи отправителя успешно созданы.");
        }
        catch (Exception ex) { _log.Add($"Ошибка: {ex.Message}"); }
    }

    private void GenerateRecipientKeys()
    {
        try
        {
            _keyService.GenerateRecipientKeys(KeyPaths);
            UpdateFingerprints();
            _log.Add("Ключи получателя успешно созданы.");
        }
        catch (Exception ex) { _log.Add($"Ошибка: {ex.Message}"); }
    }

    private void OpenKeysFolder()
    {
        try
        {
            string path = Path.GetDirectoryName(KeyPaths.SenderPublicPath) ?? "";
            if (Directory.Exists(path))
                Process.Start("explorer.exe", path);
        }
        catch (Exception ex) { _log.Add($"Ошибка: {ex.Message}"); }
    }

    private void OpenOutputFolder()
    {
        try
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoMail");
            string path = Path.Combine(appData, "Output");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start("explorer.exe", path);
        }
        catch (Exception ex) { _log.Add($"Ошибка: {ex.Message}"); }
    }

    private void AddTrustedKey()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PEM files (*.pem)|*.pem|All files (*.*)|*.*",
            Title = "Выберите публичный ключ отправителя"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string pem = File.ReadAllText(dialog.FileName);
                if (!pem.Contains("PUBLIC KEY"))
                {
                    MessageBox.Show("Выбранный файл не является публичным ключом RSA.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoMail");
                string trustedDir = Path.Combine(appData, "TrustedSenders");
                Directory.CreateDirectory(trustedDir);

                string destPath = Path.Combine(trustedDir, "trusted_sender.pem");
                File.WriteAllText(destPath, pem);

                TrustedKeyPath = destPath;
                TrustedFingerprint = _keyService.GetFingerprint(pem);
                _log.Add("Доверенный ключ успешно добавлен.");
            }
            catch (Exception ex) { _log.Add($"Ошибка при добавлении ключа: {ex.Message}"); }
        }
    }

    private void LoadTrustedKeyInfo()
    {
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoMail");
        string path = Path.Combine(appData, "TrustedSenders", "trusted_sender.pem");
        if (File.Exists(path))
        {
            TrustedKeyPath = path;
            TrustedFingerprint = _keyService.GetFingerprint(File.ReadAllText(path));
        }
    }

    private void UpdateFingerprints()
    {
        if (File.Exists(KeyPaths.SenderPublicPath))
            SenderFingerprint = _keyService.GetFingerprint(_keyService.LoadPem(KeyPaths.SenderPublicPath));
        
        if (File.Exists(KeyPaths.RecipientPublicPath))
            RecipientFingerprint = _keyService.GetFingerprint(_keyService.LoadPem(KeyPaths.RecipientPublicPath));
    }
}
