using MailKit.Net.Smtp;
using MailKit.Security;
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

        public EmailNotificationService(
            ILogger<EmailNotificationService> logger,
            IOptions<EmailSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;
        }

        public async Task SendTicketReceivedAsync(Ticket ticket)
        {
            if (string.IsNullOrWhiteSpace(ticket.RequesterEmail))
            {
                return;
            }

            var subject = $"Ticket received: TK-{ticket.Id:0000}";
            var body =
                $"Hello {ticket.Requester},\n\n" +
                "We received your request and created a helpdesk ticket.\n\n" +
                $"Ticket ID: TK-{ticket.Id:0000}\n" +
                $"Title: {ticket.Title}\n\n" +
                "Our team will review it and follow up.\n\n" +
                "Safi Helpdesk";

            await SendAsync(ticket.RequesterEmail, ticket.Requester, subject, body);
        }

        public async Task SendTicketAssignedAsync(Ticket ticket, User assignee)
        {
            if (string.IsNullOrWhiteSpace(assignee.Email))
            {
                return;
            }

            var subject = $"Ticket assigned to you: TK-{ticket.Id:0000}";
            var body =
                $"Hello {assignee.Name},\n\n" +
                "A ticket has been assigned to you.\n\n" +
                $"Ticket ID: TK-{ticket.Id:0000}\n" +
                $"Title: {ticket.Title}\n" +
                $"Requester: {ticket.Requester}\n\n" +
                "Please open the helpdesk dashboard to review it.\n\n" +
                "Safi Helpdesk";

            await SendAsync(assignee.Email, assignee.Name, subject, body);
        }

        public async Task SendTicketReplyAsync(Ticket ticket, TicketComment reply)
        {
            if (string.IsNullOrWhiteSpace(ticket.RequesterEmail))
            {
                return;
            }

            var subject = $"Re: [TK-{ticket.Id:0000}] {ticket.Title}";
            var body =
                $"Hello {ticket.Requester},\n\n" +
                $"{reply.Body}\n\n" +
                $"Ticket ID: TK-{ticket.Id:0000}\n\n" +
                "Safi Helpdesk";

            await SendAsync(ticket.RequesterEmail, ticket.Requester, subject, body);
        }

        public async Task SendTicketResolvedAsync(Ticket ticket)
        {
            if (string.IsNullOrWhiteSpace(ticket.RequesterEmail))
            {
                return;
            }

            var subject = $"Resolved: [TK-{ticket.Id:0000}] {ticket.Title}";
            var body =
                $"Hello {ticket.Requester},\n\n" +
                "Your helpdesk ticket has been reviewed and marked as resolved.\n\n" +
                $"Ticket ID: TK-{ticket.Id:0000}\n" +
                $"Title: {ticket.Title}\n\n" +
                "If the issue happens again, please reply to this email or open a new ticket.\n\n" +
                "Safi Helpdesk";

            await SendAsync(ticket.RequesterEmail, ticket.Requester, subject, body);
        }

        public async Task SendPasswordResetCodeAsync(User user, string resetCode)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            var subject = "Reset your Safi Helpdesk password";
            var body =
                $"Hello {user.Name},\n\n" +
                "A password reset was requested for your Safi Helpdesk account.\n\n" +
                $"Your reset code is: {resetCode}\n\n" +
                "Enter this code in the Helpdesk sign-in page to choose a new password.\n" +
                "This code expires in 10 minutes. If you did not request this, you can ignore this email.\n\n" +
                "Safi Helpdesk";

            await SendAsync(user.Email, user.Name, subject, body);
        }

        private async Task SendAsync(
            string toEmail,
            string? toName,
            string subject,
            string body)
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
            message.Body = new TextPart("plain")
            {
                Text = body
            };

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
    }
}
