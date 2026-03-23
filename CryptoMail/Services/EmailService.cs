using System.IO;
using System.Linq;
using CryptoMail.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace CryptoMail.Services;

public sealed class EmailService
{
    private const int MailOperationTimeoutMs = 15000;
    private const int MaxMessagesToInspect = 25;

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
        smtp.Timeout = MailOperationTimeoutMs;
        smtp.CheckCertificateRevocation = true;

        SecureSocketOptions options = ResolveSecureSocketOptions(settings.SmtpPort, 465);

        await smtp.ConnectAsync(settings.SmtpHost, settings.SmtpPort, options, cancellationToken);
        await smtp.AuthenticateAsync(settings.Login, settings.Password, cancellationToken);
        await smtp.SendAsync(message, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }

    public async Task<string> DownloadLatestAttachmentAsync(
        EmailSettings settings,
        string subjectFilter,
        CancellationToken cancellationToken = default,
        Action<string>? progress = null)
    {
        var candidates = await DownloadMatchingAttachmentsAsync(settings, subjectFilter, cancellationToken, progress);
        return candidates.FirstOrDefault().Content
            ?? throw new InvalidOperationException("Не найдено подходящих сообщений с защищенным вложением.");
    }

    public async Task<IReadOnlyList<(string Subject, string AttachmentName, string Content)>> DownloadMatchingAttachmentsAsync(
        EmailSettings settings,
        string subjectFilter,
        CancellationToken cancellationToken = default,
        Action<string>? progress = null)
    {
        ValidateImapSettings(settings);

        try
        {
            return await DownloadMatchingAttachmentsCoreAsync(settings, subjectFilter, cancellationToken, progress);
        }
        catch (Exception ex) when (IsUnexpectedDisconnect(ex))
        {
            progress?.Invoke("IMAP: соединение оборвалось, выполняю повторную попытку...");
            await Task.Delay(500, cancellationToken);
            return await DownloadMatchingAttachmentsCoreAsync(settings, subjectFilter, cancellationToken, progress);
        }
    }

    private async Task<IReadOnlyList<(string Subject, string AttachmentName, string Content)>> DownloadMatchingAttachmentsCoreAsync(
        EmailSettings settings,
        string subjectFilter,
        CancellationToken cancellationToken,
        Action<string>? progress)
    {
        using var imap = new ImapClient();
        imap.Timeout = MailOperationTimeoutMs;
        imap.CheckCertificateRevocation = true;

        SecureSocketOptions options = ResolveSecureSocketOptions(settings.ImapPort, 993);
        var matches = new List<(string Subject, string AttachmentName, string Content)>();
        var normalizedSubjectFilter = subjectFilter.Trim();

        try
        {
            progress?.Invoke($"IMAP: подключение к {settings.ImapHost}:{settings.ImapPort}...");
            await imap.ConnectAsync(settings.ImapHost, settings.ImapPort, options, cancellationToken);

            progress?.Invoke("IMAP: авторизация...");
            await imap.AuthenticateAsync(settings.Login, settings.Password, cancellationToken);

            var inbox = imap.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

            progress?.Invoke($"IMAP: поиск писем по теме \"{normalizedSubjectFilter}\"...");
            var matchedUids = await inbox.SearchAsync(SearchQuery.SubjectContains(normalizedSubjectFilter), cancellationToken);
            var orderedUids = matchedUids
                .OrderByDescending(uid => uid.Id)
                .Take(MaxMessagesToInspect)
                .ToList();

            if (matchedUids.Count > orderedUids.Count)
            {
                progress?.Invoke($"IMAP: найдено {matchedUids.Count} писем, для ускорения проверяю только последние {orderedUids.Count}.");
            }
            else
            {
                progress?.Invoke($"IMAP: найдено писем для проверки: {orderedUids.Count}.");
            }

            foreach (var uid in orderedUids)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var msg = await inbox.GetMessageAsync(uid, cancellationToken);
                var subject = msg.Subject ?? string.Empty;

                foreach (var attachment in msg.Attachments.OfType<MimePart>().Where(IsProtectedAttachment))
                {
                    using var ms = new MemoryStream();
                    await attachment.Content.DecodeToAsync(ms, cancellationToken);

                    matches.Add((
                        subject,
                        attachment.FileName ?? "envelope.json",
                        System.Text.Encoding.UTF8.GetString(ms.ToArray())));
                }
            }

            return matches;
        }
        finally
        {
            if (imap.IsConnected)
            {
                try
                {
                    await imap.DisconnectAsync(true, cancellationToken);
                }
                catch
                {
                    // Сервер уже мог разорвать соединение сам; не мешаем основной логике.
                }
            }
        }
    }

    private static SecureSocketOptions ResolveSecureSocketOptions(int port, int implicitSslPort)
        => port == implicitSslPort ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

    private static bool IsProtectedAttachment(MimePart part)
        => part.FileName != null &&
           part.FileName.Equals("envelope.json", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnexpectedDisconnect(Exception ex)
        => ex.Message.Contains("unexpectedly disconnected", StringComparison.OrdinalIgnoreCase);

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
