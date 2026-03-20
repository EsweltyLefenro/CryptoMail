using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MimeKit;
using CryptoMail.Models;
using System.IO;
using System.Linq;
using MailKit.Security;

namespace CryptoMail.Services;

public sealed class EmailService
{
    private void ConfigureSsl(IMailService client, bool ignoreErrors)
    {
        if (ignoreErrors)
        {
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
        }
    }

    public async Task SendAsync(
        EmailSettings settings,
        string to,
        string subject,
        string attachmentName,
        string attachmentText,
        CancellationToken cancellationToken = default)
    {
        ValidateSmtpSettings(settings);
        if (string.IsNullOrWhiteSpace(to))
        {
            throw new ArgumentException("Email получателя обязателен.", nameof(to));
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = string.IsNullOrWhiteSpace(subject) ? "CryptoMail package" : subject;

        var bodyBuilder = new BodyBuilder
        {
            TextBody = "Вам отправлен защищенный пакет CryptoMail. Используйте приложение для расшифровки."
        };
        bodyBuilder.Attachments.Add(attachmentName, System.Text.Encoding.UTF8.GetBytes(attachmentText));
        message.Body = bodyBuilder.ToMessageBody();

        using var smtp = new SmtpClient();
        ConfigureSsl(smtp, settings.IgnoreSslErrors);
        
        SecureSocketOptions options;
        if (!settings.UseSsl)
        {
            options = SecureSocketOptions.None;
        }
        else
        {
            options = settings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        }

        await smtp.ConnectAsync(settings.SmtpHost, settings.SmtpPort, options, cancellationToken);
        await smtp.AuthenticateAsync(settings.Login, settings.Password, cancellationToken);
        await smtp.SendAsync(message, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }

    public async Task<string> DownloadLatestAttachmentAsync(
        EmailSettings settings,
        string subjectFilter,
        CancellationToken cancellationToken = default)
    {
        ValidateImapSettings(settings);

        using var imap = new ImapClient();
        ConfigureSsl(imap, settings.IgnoreSslErrors);
        
        SecureSocketOptions options;
        if (!settings.UseSsl)
        {
            options = SecureSocketOptions.None;
        }
        else
        {
            options = settings.ImapPort == 993 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        }

        await imap.ConnectAsync(settings.ImapHost, settings.ImapPort, options, cancellationToken);
        await imap.AuthenticateAsync(settings.Login, settings.Password, cancellationToken);

        var inbox = imap.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        for (var i = inbox.Count - 1; i >= 0; i--)
        {
            var msg = await inbox.GetMessageAsync(i, cancellationToken);
            var subject = msg.Subject ?? string.Empty;
            
            if (!subject.Contains(subjectFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            // Ищем конкретно envelope.json или любой .json
            var attachment = msg.Attachments.OfType<MimePart>().FirstOrDefault(p => 
                p.FileName != null && (p.FileName.Equals("envelope.json", StringComparison.OrdinalIgnoreCase) || p.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)));
            
            if (attachment is null) continue;

            using var ms = new MemoryStream();
            await attachment.Content.DecodeToAsync(ms, cancellationToken);
            await imap.DisconnectAsync(true, cancellationToken);
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }

        await imap.DisconnectAsync(true, cancellationToken);
        throw new InvalidOperationException("Не найдено подходящих сообщений с защищенным вложением.");
    }

    private static void ValidateSmtpSettings(EmailSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SmtpHost)) throw new ArgumentException("SMTP хост обязателен.");
        if (settings.SmtpPort <= 0) throw new ArgumentException("Некорректный SMTP порт.");
        if (string.IsNullOrWhiteSpace(settings.Login) || string.IsNullOrWhiteSpace(settings.Password)) throw new ArgumentException("Логин и пароль обязательны.");
        if (string.IsNullOrWhiteSpace(settings.FromAddress)) throw new ArgumentException("Email отправителя обязателен.");
    }

    private static void ValidateImapSettings(EmailSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ImapHost)) throw new ArgumentException("IMAP хост обязателен.");
        if (settings.ImapPort <= 0) throw new ArgumentException("Некорректный IMAP порт.");
        if (string.IsNullOrWhiteSpace(settings.Login) || string.IsNullOrWhiteSpace(settings.Password)) throw new ArgumentException("Логин и пароль обязательны.");
    }
}
