using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Safi_Ticket.Data;
using Safi_Ticket.Models;

namespace Safi_Ticket.Services
{
    public class EmailIngestionService
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailNotificationService _emailNotificationService;
        private readonly TicketEventNotifier _ticketEventNotifier;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<EmailIngestionService> _logger;

        public EmailIngestionService(
            ApplicationDbContext context,
            EmailNotificationService emailNotificationService,
            TicketEventNotifier ticketEventNotifier,
            IWebHostEnvironment environment,
            ILogger<EmailIngestionService> logger
        )
        {
            _context = context;
            _emailNotificationService = emailNotificationService;
            _ticketEventNotifier = ticketEventNotifier;
            _environment = environment;
            _logger = logger;
        }

        public async Task<Ticket?> IngestEmailAsync(MimeMessage message, string fallbackMessageId)
        {
            var messageId = string.IsNullOrWhiteSpace(message.MessageId)
                ? fallbackMessageId
                : message.MessageId;

            var processedEmail = await _context.EmailMessages.FirstOrDefaultAsync(e =>
                e.MessageId == messageId
            );

            if (processedEmail != null)
            {
                await SaveEmailAttachmentsIfMissingAsync(processedEmail.TicketId, message);
                return null;
            }

            var fromMailbox = message.From.Mailboxes.FirstOrDefault();
            var fromEmail = fromMailbox?.Address ?? string.Empty;
            var fromName = string.IsNullOrWhiteSpace(fromMailbox?.Name) ? null : fromMailbox.Name;
            var subject = (message.Subject ?? string.Empty).Trim();
            var body = CleanEmailBody(message.TextBody ?? message.HtmlBody ?? string.Empty);
            var receivedAt =
                message.Date == DateTimeOffset.MinValue
                    ? DateTime.UtcNow
                    : message.Date.UtcDateTime;
            var existingTicket = await FindExistingTicketAsync(message, subject);

            if (existingTicket != null)
            {
                await ReopenTicketIfRequesterRepliedAsync(existingTicket, fromEmail);

                var replyComment = new TicketComment
                {
                    TicketId = existingTicket.Id,
                    Body = body,
                    AuthorName = fromName,
                    AuthorEmail = fromEmail,
                    AuthorType = "Requester",
                    IsInternalNote = false,
                    CreatedAt = DateTime.UtcNow,
                };

                var replyEmailMessage = new EmailMessage
                {
                    TicketId = existingTicket.Id,
                    TicketComment = replyComment,
                    MessageId = messageId,
                    FromEmail = fromEmail,
                    FromName = fromName,
                    Subject = subject,
                    Body = body,
                    ReceivedAt = receivedAt,
                    ProcessedAt = DateTime.UtcNow,
                };

                _context.TicketComments.Add(replyComment);
                _context.EmailMessages.Add(replyEmailMessage);
                await _context.SaveChangesAsync();
                await SaveEmailAttachmentsAsync(existingTicket.Id, message);

                _ticketEventNotifier.Publish(
                    new TicketEvent(
                        "ticket-comment-added",
                        existingTicket.Id,
                        replyComment.Id,
                        $"New email reply on TK-{existingTicket.Id}",
                        replyComment.CreatedAt
                    )
                );

                _logger.LogInformation(
                    "Incoming email {MessageId} added to ticket {TicketId} conversation.",
                    messageId,
                    existingTicket.Id
                );

                return existingTicket;
            }

            var ticket = new Ticket
            {
                Title = subject,
                Body = body,
                Requester = fromName ?? fromEmail,
                RequesterEmail = fromEmail,
                StatusId = 1,
                PriorityId = null,
                CreatedAt = DateTime.UtcNow,
            };

            var comment = new TicketComment
            {
                Ticket = ticket,
                Body = body,
                AuthorName = fromName,
                AuthorEmail = fromEmail,
                AuthorType = "Requester",
                IsInternalNote = false,
                CreatedAt = DateTime.UtcNow,
            };

            var emailMessage = new EmailMessage
            {
                Ticket = ticket,
                TicketComment = comment,
                MessageId = messageId,
                FromEmail = fromEmail,
                FromName = fromName,
                Subject = subject,
                Body = body,
                ReceivedAt = receivedAt,
                ProcessedAt = DateTime.UtcNow,
            };

            _context.Tickets.Add(ticket);
            _context.TicketComments.Add(comment);
            _context.EmailMessages.Add(emailMessage);

            await _context.SaveChangesAsync();
            await SaveEmailAttachmentsAsync(ticket.Id, message);

            _ticketEventNotifier.Publish(
                new TicketEvent(
                    "ticket-created",
                    ticket.Id,
                    comment.Id,
                    $"New ticket TK-{ticket.Id} received by email",
                    ticket.CreatedAt
                )
            );

            try
            {
                await _emailNotificationService.SendTicketReceivedAsync(ticket);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to send ticket received notification for ticket {TicketId}.",
                    ticket.Id
                );
            }

            return ticket;
        }

        private async Task SaveEmailAttachmentsAsync(int ticketId, MimeMessage message)
        {
            var attachmentParts = message
                .BodyParts.OfType<MimePart>()
                .Where(part =>
                    !string.IsNullOrWhiteSpace(part.FileName)
                    && (
                        part.IsAttachment
                        || string.Equals(
                            part.ContentDisposition?.Disposition,
                            ContentDisposition.Inline,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                )
                .ToList();

            if (attachmentParts.Count == 0)
            {
                return;
            }

            var webRootPath = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            var ticketFolder = Path.Combine(webRootPath, "uploads", "tickets", ticketId.ToString());
            Directory.CreateDirectory(ticketFolder);

            var attachments = new List<TicketAttachment>();

            foreach (var part in attachmentParts)
            {
                if (part.Content == null)
                {
                    continue;
                }

                var originalFileName = Path.GetFileName(part.FileName) ?? "attachment";
                var extension = Path.GetExtension(originalFileName);
                var storedFileName = $"{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(ticketFolder, storedFileName);

                await using (var stream = File.Create(filePath))
                {
                    part.Content.DecodeTo(stream);
                }

                var fileInfo = new FileInfo(filePath);
                attachments.Add(
                    new TicketAttachment
                    {
                        TicketId = ticketId,
                        FileName = originalFileName,
                        StoredFileName = storedFileName,
                        ContentType = part.ContentType?.MimeType ?? "application/octet-stream",
                        SizeBytes = fileInfo.Length,
                        FilePath = filePath,
                        Url = $"/uploads/tickets/{ticketId}/{storedFileName}",
                        UploadedAt = DateTime.UtcNow,
                    }
                );
            }

            _context.TicketAttachments.AddRange(attachments);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Saved {AttachmentCount} email attachments for ticket {TicketId}.",
                attachments.Count,
                ticketId
            );
        }

        private async Task SaveEmailAttachmentsIfMissingAsync(int ticketId, MimeMessage message)
        {
            var hasAttachments = await _context.TicketAttachments.AnyAsync(attachment =>
                attachment.TicketId == ticketId
            );

            if (hasAttachments)
            {
                return;
            }

            await SaveEmailAttachmentsAsync(ticketId, message);
        }

        private async Task<Ticket?> FindExistingTicketAsync(MimeMessage message, string subject)
        {
            var ticketIdFromSubject = TryGetTicketIdFromSubject(subject);
            if (ticketIdFromSubject.HasValue)
            {
                var ticket = await _context.Tickets.FirstOrDefaultAsync(existingTicket =>
                    existingTicket.Id == ticketIdFromSubject.Value && !existingTicket.IsDeleted
                );

                if (ticket != null)
                {
                    return ticket;
                }
            }

            var relatedMessageIds = message
                .References.Append(message.InReplyTo)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct()
                .ToList();

            if (relatedMessageIds.Count == 0)
            {
                return null;
            }

            var relatedTicketId = await _context
                .EmailMessages.AsNoTracking()
                .Where(emailMessage => relatedMessageIds.Contains(emailMessage.MessageId))
                .Select(emailMessage => (int?)emailMessage.TicketId)
                .FirstOrDefaultAsync();

            if (!relatedTicketId.HasValue)
            {
                return null;
            }

            return await _context.Tickets.FirstOrDefaultAsync(ticket =>
                ticket.Id == relatedTicketId.Value && !ticket.IsDeleted
            );
        }

        private async Task ReopenTicketIfRequesterRepliedAsync(Ticket ticket, string fromEmail)
        {
            if (
                string.IsNullOrWhiteSpace(ticket.RequesterEmail)
                || !string.Equals(
                    ticket.RequesterEmail,
                    fromEmail,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return;
            }

            var currentStatus = await _context
                .Statuses.AsNoTracking()
                .FirstOrDefaultAsync(status => status.Id == ticket.StatusId);

            if (currentStatus?.Name != "Closed")
            {
                return;
            }

            var initiatedStatus = await _context
                .Statuses.AsNoTracking()
                .FirstOrDefaultAsync(status => status.Name == "Initiated");

            if (initiatedStatus != null)
            {
                ticket.StatusId = initiatedStatus.Id;
            }
        }

        private static int? TryGetTicketIdFromSubject(string subject)
        {
            var match = Regex.Match(subject, @"TK-(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return int.TryParse(match.Groups[1].Value, out var ticketId) ? ticketId : null;
        }

        private static string CleanEmailBody(string body)
        {
            var normalizedBody = body.Replace("\r\n", "\n").Replace("\r", "\n").Trim();

            if (string.IsNullOrWhiteSpace(normalizedBody))
            {
                return string.Empty;
            }

            var lines = normalizedBody.Split('\n');
            var replyLines = new List<string>();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (
                    trimmedLine.StartsWith(">", StringComparison.Ordinal)
                    || trimmedLine.StartsWith("On ", StringComparison.OrdinalIgnoreCase)
                        && trimmedLine.EndsWith("wrote:", StringComparison.OrdinalIgnoreCase)
                    || trimmedLine.StartsWith(
                        "-----Original Message-----",
                        StringComparison.OrdinalIgnoreCase
                    )
                    || trimmedLine.StartsWith("From:", StringComparison.OrdinalIgnoreCase)
                    || trimmedLine.StartsWith("Sent:", StringComparison.OrdinalIgnoreCase)
                    || trimmedLine.StartsWith("To:", StringComparison.OrdinalIgnoreCase)
                    || trimmedLine.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase)
                )
                {
                    break;
                }

                if (
                    trimmedLine.Equals("Sent from my iPhone", StringComparison.OrdinalIgnoreCase)
                    || trimmedLine.Equals("Sent from my iPad", StringComparison.OrdinalIgnoreCase)
                    || trimmedLine.StartsWith("Sent from my ", StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }

                replyLines.Add(line.TrimEnd());
            }

            var cleanedBody = string.Join('\n', replyLines).Trim();

            return string.IsNullOrWhiteSpace(cleanedBody) ? normalizedBody : cleanedBody;
        }
    }
}
