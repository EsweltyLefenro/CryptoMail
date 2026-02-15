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
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

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
        using var imap = new ImapClient();
        await imap.ConnectAsync(settings.ImapHost, settings.ImapPort, settings.UseSsl, cancellationToken);
        await imap.AuthenticateAsync(settings.Login, settings.Password, cancellationToken);

        var inbox = imap.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        for (var i = inbox.Count - 1; i >= 0; i--)
        {
            var msg = await inbox.GetMessageAsync(i, cancellationToken);
            if (!msg.Subject.Contains(subjectFilter, StringComparison.OrdinalIgnoreCase))
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
}
