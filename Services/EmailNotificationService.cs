using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;
using Safi_Ticket.DTO.Settings;
using Safi_Ticket.Models;

namespace Safi_Ticket.Services
{
    public class EmailNotificationService
    {
        private readonly ILogger<EmailNotificationService> _logger;
        private readonly EmailSettings _settings;
        private readonly IWebHostEnvironment _environment;

        public EmailNotificationService(
            ILogger<EmailNotificationService> logger,
            IOptions<EmailSettings> settings,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _settings = settings.Value;
            _environment = environment;
        }

        public async Task SendTicketReceivedAsync(Ticket ticket)
        {
            if (string.IsNullOrWhiteSpace(ticket.RequesterEmail))
            {
                return;
            }

            var subject = $"Ticket received: TK-{ticket.Id}";
            var plainBody =
                $"Hello {ticket.Requester},\n\n" +
                "We received your request and created a helpdesk ticket.\n\n" +
                $"Ticket ID: TK-{ticket.Id}\n" +
                $"Title: {ticket.Title}\n\n" +
                "Our team will review it and follow up.\n\n" +
                "Safi Helpdesk";
            var htmlBody = BuildBrandedEmailHtml(
                "Ticket received",
                $"TK-{ticket.Id}",
                ticket.Title,
                ticket.Requester,
                "We received your request and created a helpdesk ticket.",
                "Our team will review it and follow up."
            );

            await SendAsync(ticket.RequesterEmail, ticket.Requester, subject, plainBody, htmlBody);
        }

        public async Task SendTicketAssignedAsync(Ticket ticket, User assignee)
        {
            if (string.IsNullOrWhiteSpace(assignee.Email))
            {
                return;
            }

            var subject = $"Ticket assigned to you: TK-{ticket.Id}";
            var plainBody =
                $"Hello {assignee.Name},\n\n" +
                "A ticket has been assigned to you.\n\n" +
                $"Ticket ID: TK-{ticket.Id}\n" +
                $"Title: {ticket.Title}\n" +
                $"Requester: {ticket.Requester}\n\n" +
                "Please open the helpdesk dashboard to review it.\n\n" +
                "Safi Helpdesk";
            var htmlBody = BuildBrandedEmailHtml(
                "Ticket assignment",
                $"TK-{ticket.Id}",
                ticket.Title,
                assignee.Name,
                "A ticket has been assigned to you.",
                new Dictionary<string, string>
                {
                    ["Requester"] = ticket.Requester,
                },
                "Please open the helpdesk dashboard to review it."
            );

            await SendAsync(assignee.Email, assignee.Name, subject, plainBody, htmlBody);
        }

        public async Task SendTicketReplyAsync(Ticket ticket, TicketComment reply)
        {
            if (string.IsNullOrWhiteSpace(ticket.RequesterEmail))
            {
                return;
            }

            var subject = $"Re: [TK-{ticket.Id}] {ticket.Title}";
            var plainBody =
                $"Hello {ticket.Requester},\n\n" +
                $"{reply.Body}\n\n" +
                $"Ticket ID: TK-{ticket.Id}\n\n" +
                "Safi Helpdesk";
            var htmlBody = BuildTicketReplyHtml(ticket, reply);

            await SendAsync(ticket.RequesterEmail, ticket.Requester, subject, plainBody, htmlBody);
        }

        public async Task SendTicketClosedAsync(Ticket ticket)
        {
            if (string.IsNullOrWhiteSpace(ticket.RequesterEmail))
            {
                return;
            }

            var subject = $"Closed: [TK-{ticket.Id}] {ticket.Title}";
            var plainBody =
                $"Hello {ticket.Requester},\n\n" +
                "Your helpdesk ticket has been reviewed and closed.\n\n" +
                $"Ticket ID: TK-{ticket.Id}\n" +
                $"Title: {ticket.Title}\n\n" +
                "If the issue happens again, please reply to this email or open a new ticket.\n\n" +
                "Safi Helpdesk";
            var htmlBody = BuildBrandedEmailHtml(
                "Ticket closed",
                $"TK-{ticket.Id}",
                ticket.Title,
                ticket.Requester,
                "Your helpdesk ticket has been reviewed and closed.",
                "If the issue happens again, please reply to this email or open a new ticket."
            );

            await SendAsync(ticket.RequesterEmail, ticket.Requester, subject, plainBody, htmlBody);
        }

        public async Task SendPasswordResetCodeAsync(User user, string resetCode)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            var subject = "Reset your Safi Helpdesk password";
            var plainBody =
                $"Hello {user.Name},\n\n" +
                "A password reset was requested for your Safi Helpdesk account.\n\n" +
                $"Your reset code is: {resetCode}\n\n" +
                "Enter this code in the Helpdesk sign-in page to choose a new password.\n" +
                "This code expires in 10 minutes. If you did not request this, you can ignore this email.\n\n" +
                "Safi Helpdesk";
            var htmlBody = BuildBrandedEmailHtml(
                "Password reset",
                "Reset your password",
                "Safi Ticketing System",
                user.Name,
                "A password reset was requested for your Safi Helpdesk account.",
                new Dictionary<string, string>
                {
                    ["Reset code"] = resetCode,
                    ["Expires"] = "10 minutes",
                },
                "Enter this code in the Helpdesk sign-in page to choose a new password. If you did not request this, you can ignore this email."
            );

            await SendAsync(user.Email, user.Name, subject, plainBody, htmlBody);
        }

        private async Task SendAsync(
            string toEmail,
            string? toName,
            string subject,
            string body,
            string? htmlBody = null)
        {
            var smtpHost = string.IsNullOrWhiteSpace(_settings.SmtpHost)
                ? _settings.Host
                : _settings.SmtpHost;

            if (
                string.IsNullOrWhiteSpace(smtpHost) ||
                _settings.SmtpPort == 0 ||
                string.IsNullOrWhiteSpace(_settings.Username) ||
                string.IsNullOrWhiteSpace(_settings.Password))
            {
                _logger.LogWarning("SMTP email notification skipped because SMTP settings are incomplete.");
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.Username));
            message.To.Add(new MailboxAddress(toName ?? toEmail, toEmail));
            message.Subject = subject;

            if (string.IsNullOrWhiteSpace(htmlBody))
            {
                message.Body = new TextPart("plain")
                {
                    Text = body
                };
            }
            else
            {
                const string logoContentId = "safi-ticket-logo@safi-ticket.local";
                var bodyBuilder = new BodyBuilder
                {
                    TextBody = body,
                    HtmlBody = htmlBody,
                };
                var logo = ResolveEmailLogo();

                if (logo != null)
                {
                    var logoResource = bodyBuilder.LinkedResources.Add(logo.Path);
                    logoResource.ContentId = logoContentId;
                    logoResource.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
                    logoResource.ContentDisposition.FileName = logo.FileName;
                    logoResource.ContentType.MediaType = "image";
                    logoResource.ContentType.MediaSubtype = logo.MediaSubtype;
                    logoResource.ContentType.Name = logo.FileName;

                    if (logoResource is MimePart logoPart)
                    {
                        logoPart.ContentTransferEncoding = ContentEncoding.Base64;
                        logoPart.FileName = logo.FileName;
                    }
                }
                else
                {
                    _logger.LogWarning("Safi email logo was not found for HTML email rendering.");
                }

                message.Body = bodyBuilder.ToMessageBody();
            }

            using var client = new SmtpClient();
            var socketOptions = _settings.SmtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(smtpHost, _settings.SmtpPort, socketOptions);
            client.AuthenticationMechanisms.Remove("XOAUTH2");
            client.AuthenticationMechanisms.Remove("OAUTHBEARER");
            await client.AuthenticateAsync(_settings.Username, _settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        private EmailLogoAsset? ResolveEmailLogo()
        {
            var webRootPath = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            var candidateLogos = new[]
            {
                new EmailLogoAsset(
                    Path.Combine(webRootPath, "email-assets", "Safi_Ticket_Logo.png"),
                    "png",
                    "Safi_Ticket_Logo.png"
                ),
                new EmailLogoAsset(
                    Path.Combine(webRootPath, "email-assets", "Safi_Ticket_Logo.jpg"),
                    "jpeg",
                    "Safi_Ticket_Logo.jpg"
                ),
                new EmailLogoAsset(
                    Path.Combine(webRootPath, "email-assets", "Safi_Ticket_Logo.jpeg"),
                    "jpeg",
                    "Safi_Ticket_Logo.jpeg"
                ),
                new EmailLogoAsset(
                    Path.Combine(webRootPath, "email-assets", "Safi_Ticket_Logo.webp"),
                    "webp",
                    "Safi_Ticket_Logo.webp"
                ),
            };

            return candidateLogos
                .Select(logo => logo with { Path = Path.GetFullPath(logo.Path) })
                .FirstOrDefault(logo => File.Exists(logo.Path));
        }

        private static string BuildTicketReplyHtml(Ticket ticket, TicketComment reply)
        {
            return BuildBrandedEmailHtml(
                "Helpdesk reply",
                $"TK-{ticket.Id}",
                ticket.Title,
                ticket.Requester,
                "A member of the Safi helpdesk team has replied to your ticket.",
                "You can reply to this email to continue the conversation.",
                "Message from " + (reply.AuthorName ?? "Safi Helpdesk"),
                reply.Body
            );
        }

        private static string BuildBrandedEmailHtml(
            string label,
            string heading,
            string title,
            string recipientName,
            string intro,
            string footerNote,
            string? messageLabel = null,
            string? messageBody = null)
        {
            return BuildBrandedEmailHtml(
                label,
                heading,
                title,
                recipientName,
                intro,
                new Dictionary<string, string>(),
                footerNote,
                messageLabel,
                messageBody
            );
        }

        private static string BuildBrandedEmailHtml(
            string label,
            string heading,
            string title,
            string recipientName,
            string intro,
            IReadOnlyDictionary<string, string> details,
            string footerNote,
            string? messageLabel = null,
            string? messageBody = null)
        {
            var safeLabel = HtmlEncode(label);
            var safeHeading = HtmlEncode(heading);
            var safeTitle = HtmlEncode(title);
            var safeRecipientName = HtmlEncode(recipientName);
            var safeIntro = HtmlEncode(intro);
            var safeFooterNote = HtmlEncode(footerNote);
            var detailsHtml = string.Join(
                string.Empty,
                details.Select(detail =>
                    $"""
                    <tr>
                      <td style="padding:6px 0;color:#6d6a63;font-size:13px;line-height:1.6;width:120px;">{HtmlEncode(detail.Key)}</td>
                      <td style="padding:6px 0;color:#191918;font-size:13px;line-height:1.6;font-weight:700;">{HtmlEncode(detail.Value)}</td>
                    </tr>
                    """
                )
            );
            var detailsSectionHtml = details.Count == 0
                ? string.Empty
                : $$"""
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:22px 0 0 0;border-top:1px solid #ebe9e4;padding-top:14px;">
                      {{detailsHtml}}
                    </table>
                    """;
            var messageHtml = string.IsNullOrWhiteSpace(messageBody)
                ? string.Empty
                : $$"""
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:22px 0 0 0;border-left:3px solid #191918;background:#f8f8f6;">
                      <tr>
                        <td style="padding:18px 20px 18px 22px;">
                          <p style="margin:0 0 10px 0;color:#6d6a63;font-size:11px;font-weight:700;letter-spacing:0.12em;text-transform:uppercase;">{{HtmlEncode(messageLabel ?? "Message")}}</p>
                          <div style="color:#242321;font-size:15px;line-height:1.75;">{{HtmlBody(messageBody)}}</div>
                        </td>
                      </tr>
                    </table>
                    """;

            return $$"""
                <!doctype html>
                <html lang="en">
                  <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <title>{{safeHeading}}</title>
                  </head>
                  <body style="margin:0;padding:0;background:#f8f8f6;color:#191918;font-family:Arial,Helvetica,sans-serif;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f8f8f6;margin:0;padding:36px 12px;">
                      <tr>
                        <td align="center">
                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:640px;background:#ffffff;border:1px solid #deddd8;border-radius:8px;overflow:hidden;box-shadow:0 16px 42px rgba(25,25,24,0.07);">
                            <tr>
                              <td style="padding:30px 36px 24px 36px;border-bottom:1px solid #e7e5df;background:#fbfbf9;">
                                <img src="cid:safi-ticket-logo@safi-ticket.local" alt="Safi Ticket" width="235" style="display:block;width:235px;max-width:100%;height:auto;margin:0 0 26px 0;">
                                <p style="margin:0 0 9px 0;color:#706d66;font-size:11px;font-weight:700;letter-spacing:0.14em;text-transform:uppercase;">{{safeLabel}}</p>
                                <h1 style="margin:0;color:#191918;font-size:28px;line-height:1.2;font-weight:700;">{{safeHeading}}</h1>
                                <p style="margin:10px 0 0 0;color:#4b4944;font-size:16px;line-height:1.55;">{{safeTitle}}</p>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:30px 36px 32px 36px;">
                                <p style="margin:0 0 16px 0;color:#191918;font-size:16px;line-height:1.7;">Hello {{safeRecipientName}},</p>
                                <p style="margin:0;color:#4b4944;font-size:16px;line-height:1.75;">{{safeIntro}}</p>
                                {{messageHtml}}
                                {{detailsSectionHtml}}
                                <p style="margin:22px 0 0 0;color:#6d6a63;font-size:13px;line-height:1.65;">{{safeFooterNote}}</p>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:18px 36px;background:#191918;color:#f8f8f6;font-size:12px;line-height:1.7;">
                                <strong style="font-size:13px;">Safi Ticketing System</strong><br>
                                IT Department Helpdesk
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                    </table>
                  </body>
                </html>
                """;
        }

        private static string HtmlEncode(string? value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static string HtmlBody(string? value)
        {
            return HtmlEncode(value)
                .Replace("\r\n", "\n")
                .Replace("\n", "<br>");
        }

        private sealed record EmailLogoAsset(string Path, string MediaSubtype, string FileName);
    }
}
