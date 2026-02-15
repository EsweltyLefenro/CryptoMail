using CryptoMail.Services;

namespace CryptoMail.ViewModels;

public sealed class MainViewModel
{
    public MainViewModel()
    {
        var keyService = new KeyService();
        var cryptoService = new CryptoService();
        var packageService = new PackageService();
        var emailService = new EmailService();
        var fileDialogService = new FileDialogService();
        var storageService = new StorageService();

        Log = new LogViewModel();

        Sender = new SenderViewModel(
            keyService,
            cryptoService,
            packageService,
            emailService,
            fileDialogService,
            storageService,
            Log);

        Receiver = new ReceiverViewModel(
            keyService,
            cryptoService,
            packageService,
            emailService,
            storageService,
            Log);
    }

    public SenderViewModel Sender { get; }

    public ReceiverViewModel Receiver { get; }

    public LogViewModel Log { get; }
}
