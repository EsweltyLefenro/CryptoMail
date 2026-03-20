using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CryptoMail.Models;

public class EmailSettings : INotifyPropertyChanged
{
    private string _smtpHost = "smtp.gmail.com";
    private int _smtpPort = 465;
    private string _imapHost = "imap.gmail.com";
    private int _imapPort = 993;
    private string _login = "";
    private string _password = "";
    private string _fromAddress = "";
    private string _subjectFilter = "CryptoMail";
    private bool _useSsl = true;
    private bool _ignoreSslErrors = false; // Безопасное значение по умолчанию

    public string SmtpHost { get => _smtpHost; set => SetProperty(ref _smtpHost, value); }
    public int SmtpPort { get => _smtpPort; set => SetProperty(ref _smtpPort, value); }
    public string ImapHost { get => _imapHost; set => SetProperty(ref _imapHost, value); }
    public int ImapPort { get => _imapPort; set => SetProperty(ref _imapPort, value); }
    public string Login { get => _login; set => SetProperty(ref _login, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string FromAddress { get => _fromAddress; set => SetProperty(ref _fromAddress, value); }
    public string SubjectFilter { get => _subjectFilter; set => SetProperty(ref _subjectFilter, value); }
    public bool UseSsl { get => _useSsl; set => SetProperty(ref _useSsl, value); }
    public bool IgnoreSslErrors { get => _ignoreSslErrors; set => SetProperty(ref _ignoreSslErrors, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
