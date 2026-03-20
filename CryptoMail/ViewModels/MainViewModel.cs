using CryptoMail.Models;
using CryptoMail.Services;
using System.ComponentModel;

namespace CryptoMail.ViewModels;

public sealed class MainViewModel : BaseViewModel
{
    private readonly LogViewModel _log;
    private object _currentView;

    public MainViewModel()
    {
        var keyService = new KeyService();
        var cryptoService = new CryptoService();
        var packageService = new PackageService();
        var emailService = new EmailService();
        var fileDialogService = new FileDialogService();
        var storageService = new StorageService();

        _log = new LogViewModel();

        Keys = new KeysViewModel(keyService, _log);

        Sender = new SenderViewModel(
            keyService,
            cryptoService,
            packageService,
            emailService,
            fileDialogService,
            storageService,
            _log);

        Receiver = new ReceiverViewModel(
            keyService,
            cryptoService,
            packageService,
            emailService,
            storageService,
            _log);

        _currentView = Sender;

        Sender.Settings.PropertyChanged += OnSettingsChanged;
        Receiver.Settings.PropertyChanged += OnSettingsChanged;

        ShowSenderCommand = new RelayCommand(_ => CurrentView = Sender);
        ShowReceiverCommand = new RelayCommand(_ => CurrentView = Receiver);
        ShowKeysCommand = new RelayCommand(_ => CurrentView = Keys);
        ClearLogCommand = new RelayCommand(_ => _log.Clear());
    }

    public SenderViewModel Sender { get; }
    public ReceiverViewModel Receiver { get; }
    public KeysViewModel Keys { get; }
    public LogViewModel Log => _log;

    public object CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public RelayCommand ShowSenderCommand { get; }
    public RelayCommand ShowReceiverCommand { get; }
    public RelayCommand ShowKeysCommand { get; }
    public RelayCommand ClearLogCommand { get; }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is EmailSettings settings && e.PropertyName == nameof(EmailSettings.Login))
        {
            AutoConfigure(settings);
        }
    }

    private void AutoConfigure(EmailSettings settings)
    {
        string login = settings.Login.ToLower();
        if (login.EndsWith("@gmail.com"))
        {
            settings.SmtpHost = "smtp.gmail.com";
            settings.SmtpPort = 465;
            settings.ImapHost = "imap.gmail.com";
            settings.ImapPort = 993;
        }
        else if (login.EndsWith("@mail.ru") || login.EndsWith("@bk.ru") || login.EndsWith("@inbox.ru") || login.EndsWith("@list.ru"))
        {
            settings.SmtpHost = "smtp.mail.ru";
            settings.SmtpPort = 465;
            settings.ImapHost = "imap.mail.ru";
            settings.ImapPort = 993;
        }
        else if (login.EndsWith("@yandex.ru"))
        {
            settings.SmtpHost = "smtp.yandex.ru";
            settings.SmtpPort = 465;
            settings.ImapHost = "imap.yandex.ru";
            settings.ImapPort = 993;
        }
    }
}
