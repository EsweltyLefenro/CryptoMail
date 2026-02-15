using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MimeKit;
using CryptoMail.Models;
using System.IO;
using System.Linq;

namespace CryptoMail.Services;

public sealed class EmailService
{
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
            throw new ArgumentException("Recipient email is required.", nameof(to));
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = string.IsNullOrWhiteSpace(subject) ? "CryptoMail package" : subject;

        var bodyBuilder = new BodyBuilder
        {
            TextBody = "CryptoMail package attached."
        };
        bodyBuilder.Attachments.Add(attachmentName, System.Text.Encoding.UTF8.GetBytes(attachmentText));
        message.Body = bodyBuilder.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(settings.SmtpHost, settings.SmtpPort, settings.UseSsl, cancellationToken);
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
        await imap.ConnectAsync(settings.ImapHost, settings.ImapPort, settings.UseSsl, cancellationToken);
        await imap.AuthenticateAsync(settings.Login, settings.Password, cancellationToken);

        var inbox = imap.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        for (var i = inbox.Count - 1; i >= 0; i--)
        {
            var msg = await inbox.GetMessageAsync(i, cancellationToken);
            var subject = msg.Subject ?? string.Empty;
            if (!subject.Contains(subjectFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var attachment = msg.Attachments.OfType<MimePart>().FirstOrDefault();
            if (attachment is null)
            {
                continue;
            }

            using var ms = new MemoryStream();
            await attachment.Content.DecodeToAsync(ms, cancellationToken);
            await imap.DisconnectAsync(true, cancellationToken);
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }

        await imap.DisconnectAsync(true, cancellationToken);
        throw new InvalidOperationException("No matching message with attachment was found.");
    }

    private static void ValidateSmtpSettings(EmailSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            throw new ArgumentException("SMTP host is required.", nameof(settings));
        }

        if (settings.SmtpPort <= 0)
        {
            throw new ArgumentException("SMTP port must be a positive number.", nameof(settings));
        }

        if (string.IsNullOrWhiteSpace(settings.Login) || string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new ArgumentException("SMTP login and password are required.", nameof(settings));
        }

        if (string.IsNullOrWhiteSpace(settings.FromAddress))
        {
            throw new ArgumentException("Sender (From) email is required.", nameof(settings));
        }
    }

    private static void ValidateImapSettings(EmailSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ImapHost))
        {
            throw new ArgumentException("IMAP host is required.", nameof(settings));
        }

        if (settings.ImapPort <= 0)
        {
            throw new ArgumentException("IMAP port must be a positive number.", nameof(settings));
        }

        if (string.IsNullOrWhiteSpace(settings.Login) || string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new ArgumentException("IMAP login and password are required.", nameof(settings));
        }
    }
}
