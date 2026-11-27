// MailKit 5: Send/SendAsync(message) sind Erweiterungsmethoden in MailKit.IMailTransportExtensions.
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PubPot.Web.Data;

namespace PubPot.Web.Email;

/// <summary>Verschickt eine Mail. Eigene Schnittstelle, damit Tests den Versand ersetzen können.</summary>
public interface IEmailTransport
{
    Task SendAsync(OutboxEmail email, CancellationToken ct);
}

/// <summary>Versand per SMTP mit MailKit (lokal an Mailpit, später an einen SMTP-Anbieter in der EU).</summary>
public sealed class SmtpEmailTransport(IOptions<SmtpOptions> options) : IEmailTransport
{
    public async Task SendAsync(OutboxEmail email, CancellationToken ct)
    {
        var smtp = options.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;
        message.Body = email.HtmlBody is null
            ? new TextPart("plain") { Text = email.TextBody }
            : new BodyBuilder { TextBody = email.TextBody, HtmlBody = email.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient { Timeout = 30_000 };
        var security = smtp.Security switch
        {
            SmtpSecurity.None => SecureSocketOptions.None,
            SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.StartTls, // erzwingt Verschlüsselung, bricht sonst ab
        };

        await client.ConnectAsync(smtp.Host, smtp.Port, security, ct);
        if (!string.IsNullOrEmpty(smtp.Username))
        {
            await client.AuthenticateAsync(smtp.Username, smtp.Password ?? "", ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
