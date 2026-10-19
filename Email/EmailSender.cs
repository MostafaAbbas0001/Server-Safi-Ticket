using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Safi_Ticket.Common.Configuration;
using Safi_Ticket.Common.Storage;
using Safi_Ticket.Models;

namespace Safi_Ticket.Email;

public sealed record OutgoingEmail
{
    public required IReadOnlyList<EmailParticipant> To { get; init; }
    public IReadOnlyList<EmailParticipant> Cc { get; init; } = [];
    public required string Subject { get; init; }
    public required string TextBody { get; init; }
    public string? HtmlBody { get; init; }

    /// <summary>Message-Id of the email being answered, so clients thread the reply.</summary>
    public string? InReplyTo { get; init; }

    /// <summary>Message-Id to send with, when it was recorded beforehand. Generated otherwise.</summary>
    public string? MessageId { get; init; }
}

/// <summary>Sends mail through the helpdesk mailbox over SMTP. Knows nothing about tickets.</summary>
public sealed class EmailSender(
    IOptions<EmailSettings> settings,
    AttachmentStorage storage,
    ILogger<EmailSender> logger
)
{
    private readonly EmailSettings _settings = settings.Value;

    public async Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default)
    {
        var to = email.To.Where(p => !_settings.IsHelpdeskAddress(p.Email)).ToList();
        var cc = email.Cc.Where(p => !_settings.IsHelpdeskAddress(p.Email)).ToList();

        if (to.Count == 0 && cc.Count == 0)
        {
            logger.LogInformation(
                "Skipped email {Subject}: no recipients outside the helpdesk.",
                email.Subject
            );
            return;
        }

        var smtpHost = string.IsNullOrWhiteSpace(_settings.SmtpHost)
            ? _settings.Host
            : _settings.SmtpHost;

        if (
            string.IsNullOrWhiteSpace(smtpHost)
            || _settings.SmtpPort == 0
            || string.IsNullOrWhiteSpace(_settings.Username)
            || string.IsNullOrWhiteSpace(_settings.Password)
        )
        {
            logger.LogWarning(
                "Skipped email {Subject}: SMTP settings are incomplete.",
                email.Subject
            );
            return;
        }

        var message = new MimeMessage { Subject = email.Subject };
        if (!string.IsNullOrWhiteSpace(email.MessageId))
        {
            message.MessageId = email.MessageId;
        }

        message.From.Add(new MailboxAddress(_settings.FromName, _settings.Username));
        message.To.AddRange(to.Select(ToMailbox));
        message.Cc.AddRange(cc.Select(ToMailbox));

        if (!string.IsNullOrWhiteSpace(email.InReplyTo))
        {
            message.InReplyTo = email.InReplyTo;
            message.References.Add(email.InReplyTo);
        }

        message.Body = BuildBody(email);

        using var client = new SmtpClient();
        var socketOptions =
            _settings.SmtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

        await client.ConnectAsync(smtpHost, _settings.SmtpPort, socketOptions, cancellationToken);
        client.AuthenticationMechanisms.Remove("XOAUTH2");
        client.AuthenticationMechanisms.Remove("OAUTHBEARER");
        await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation(
            "Sent email {Subject} to {ToCount} recipient(s), copying {CcCount}.",
            email.Subject,
            to.Count,
            cc.Count
        );
    }

    private MimeEntity BuildBody(OutgoingEmail email)
    {
        if (string.IsNullOrWhiteSpace(email.HtmlBody))
        {
            return new TextPart("plain") { Text = email.TextBody };
        }

        var builder = new BodyBuilder { TextBody = email.TextBody, HtmlBody = email.HtmlBody };
        var logoPath = storage.GetWebRootFile("email-assets", "Safi_Ticket_Logo.png");

        if (File.Exists(logoPath))
        {
            var logo = builder.LinkedResources.Add(logoPath);
            logo.ContentId = EmailTemplates.LogoContentId;
            logo.ContentDisposition = new ContentDisposition(ContentDisposition.Inline)
            {
                FileName = "Safi_Ticket_Logo.png",
            };
        }
        else
        {
            logger.LogWarning("Email logo not found at {LogoPath}.", logoPath);
        }

        return builder.ToMessageBody();
    }

    private static MailboxAddress ToMailbox(EmailParticipant participant) =>
        new(participant.Name ?? participant.Email, participant.Email);
}
