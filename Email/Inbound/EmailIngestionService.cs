using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using Safi_Ticket.Common.Configuration;
using Safi_Ticket.Common.Storage;
using Safi_Ticket.Data;
using Safi_Ticket.Models;

namespace Safi_Ticket.Email.Inbound;

/// <summary>
/// Turns one incoming email into ticket data: a new ticket, or a new message
/// on the ticket it answers. Records who the email was to and copied to, so
/// replies can go back to everyone on the thread.
/// </summary>
public sealed partial class EmailIngestionService(
    ApplicationDbContext context,
    TicketNotifications notifications,
    AttachmentStorage storage,
    IOptions<EmailSettings> settings,
    ILogger<EmailIngestionService> logger
)
{
    private readonly EmailSettings _settings = settings.Value;

    public async Task<Ticket?> IngestEmailAsync(MimeMessage message, string fallbackMessageId)
    {
        var messageId = string.IsNullOrWhiteSpace(message.MessageId)
            ? fallbackMessageId
            : message.MessageId;

        var processedEmail = await context.EmailMessages.FirstOrDefaultAsync(e =>
            e.MessageId == messageId
        );

        if (processedEmail != null)
        {
            await SaveAttachmentsIfTicketHasNoneAsync(processedEmail.TicketId, message);
            return null;
        }

        var from = message.From.Mailboxes.FirstOrDefault();
        var fromEmail = from?.Address ?? string.Empty;
        var fromName = string.IsNullOrWhiteSpace(from?.Name) ? null : from.Name.Trim();
        var subject = (message.Subject ?? string.Empty).Trim();

        if (_settings.IsHelpdeskAddress(fromEmail))
        {
            logger.LogInformation(
                "Skipped helpdesk's own email {MessageId} ({Subject}).",
                messageId,
                subject
            );
            return null;
        }

        var body = EmailBodyCleaner.Clean(message.TextBody, message.HtmlBody);
        var receivedAt =
            message.Date == DateTimeOffset.MinValue ? DateTime.UtcNow : message.Date.UtcDateTime;
        var comment = new TicketComment
        {
            Body = body,
            AuthorName = fromName,
            AuthorEmail = fromEmail,
            AuthorType = TicketComment.RequesterAuthor,
            IsInternalNote = false,
            CreatedAt = receivedAt,
            ToRecipients = EmailRecipients.FromMime(message.To),
            CcRecipients = EmailRecipients.FromMime(message.Cc),
        };
        var emailRecord = new EmailMessage
        {
            TicketComment = comment,
            MessageId = messageId,
            FromEmail = fromEmail,
            FromName = fromName,
            Subject = subject,
            Body = body,
            ReceivedAt = receivedAt,
            ProcessedAt = DateTime.UtcNow,
        };

        var existingTicket = await FindTicketForReplyAsync(message, subject);

        if (existingTicket != null)
        {
            await ReopenIfRequesterRepliedAsync(existingTicket, fromEmail);
            existingTicket.RequesterReplyVersion += 1;
            existingTicket.LastRequesterReplyAt = receivedAt;

            comment.TicketId = existingTicket.Id;
            emailRecord.TicketId = existingTicket.Id;
            context.TicketComments.Add(comment);
            context.EmailMessages.Add(emailRecord);
            await context.SaveChangesAsync();
            await SaveAttachmentsAsync(existingTicket.Id, message);

            logger.LogInformation(
                "Email {MessageId} added to ticket {TicketId}.",
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
            StatusId = await GetStatusIdAsync(TicketStatuses.Initiated),
            CreatedAt = receivedAt,
        };

        comment.Ticket = ticket;
        emailRecord.Ticket = ticket;
        context.Tickets.Add(ticket);
        context.TicketComments.Add(comment);
        context.EmailMessages.Add(emailRecord);
        await context.SaveChangesAsync();
        await SaveAttachmentsAsync(ticket.Id, message);

        try
        {
            await notifications.SendTicketReceivedAsync(ticket);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Couldn't send the 'received' email for ticket {TicketId}.",
                ticket.Id
            );
        }

        return ticket;
    }

    private async Task SaveAttachmentsAsync(int ticketId, MimeMessage message)
    {
        var parts = message
            .BodyParts.OfType<MimePart>()
            .Where(part => part.Content != null && (IsFileAttachment(part) || IsInlineImage(part)))
            .ToList();

        if (parts.Count == 0)
        {
            return;
        }

        foreach (var (part, index) in parts.Select((part, index) => (part, index)))
        {
            // Pasted screenshots often arrive with a Content-Id but no file name.
            var fileName = string.IsNullOrWhiteSpace(part.FileName)
                ? $"image-{index + 1}.{part.ContentType?.MediaSubtype ?? "png"}"
                : part.FileName;

            var attachment = await storage.SaveAsync(
                ticketId,
                fileName,
                part.ContentType?.MimeType,
                stream =>
                {
                    part.Content!.DecodeTo(stream);
                    return Task.CompletedTask;
                }
            );

            // Lets the web app swap the body's "cid:" image references for this file.
            attachment.ContentId = string.IsNullOrWhiteSpace(part.ContentId)
                ? null
                : part.ContentId.Trim('<', '>');
            attachment.IsInline = IsInlineImage(part);
            context.TicketAttachments.Add(attachment);
        }

        await context.SaveChangesAsync();
        logger.LogInformation(
            "Saved {Count} attachment(s) for ticket {TicketId}.",
            parts.Count,
            ticketId
        );
    }

    private static bool IsFileAttachment(MimePart part) =>
        !string.IsNullOrWhiteSpace(part.FileName)
        && (
            part.IsAttachment
            || string.Equals(
                part.ContentDisposition?.Disposition,
                ContentDisposition.Inline,
                StringComparison.OrdinalIgnoreCase
            )
        );

    /// <summary>An image shown inside the email body (signature logo, pasted screenshot).</summary>
    private static bool IsInlineImage(MimePart part) =>
        !string.IsNullOrWhiteSpace(part.ContentId)
        && part.ContentType?.MediaType == "image"
        && !part.IsAttachment;

    /// <summary>Recovers attachments when an email was recorded but its files weren't saved.</summary>
    private async Task SaveAttachmentsIfTicketHasNoneAsync(int ticketId, MimeMessage message)
    {
        if (!await context.TicketAttachments.AnyAsync(a => a.TicketId == ticketId))
        {
            await SaveAttachmentsAsync(ticketId, message);
        }
    }

    /// <summary>
    /// The ticket an email answers: by the [TK-123] tag in its subject, or by
    /// the Message-Ids it references (for clients that rewrite the subject).
    /// </summary>
    private async Task<Ticket?> FindTicketForReplyAsync(MimeMessage message, string subject)
    {
        var tagMatch = TicketTagPattern().Match(subject);
        if (tagMatch.Success && int.TryParse(tagMatch.Groups[1].Value, out var taggedId))
        {
            var tagged = await context.Tickets.FirstOrDefaultAsync(t =>
                t.Id == taggedId && !t.IsDeleted
            );
            if (tagged != null)
            {
                return tagged;
            }
        }

        var referencedIds = message
            .References.Append(message.InReplyTo)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (referencedIds.Count == 0)
        {
            return null;
        }

        var ticketId = await context
            .EmailMessages.AsNoTracking()
            .Where(e => referencedIds.Contains(e.MessageId))
            .Select(e => (int?)e.TicketId)
            .FirstOrDefaultAsync();

        return ticketId.HasValue
            ? await context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId.Value && !t.IsDeleted)
            : null;
    }

    /// <summary>A closed ticket goes back to Initiated when its requester writes again.</summary>
    private async Task ReopenIfRequesterRepliedAsync(Ticket ticket, string fromEmail)
    {
        if (!string.Equals(ticket.RequesterEmail, fromEmail, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var statusName = await context
            .Statuses.AsNoTracking()
            .Where(s => s.Id == ticket.StatusId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync();

        if (statusName == TicketStatuses.Closed)
        {
            ticket.StatusId = await GetStatusIdAsync(TicketStatuses.Initiated);
        }
    }

    private async Task<int> GetStatusIdAsync(string statusName) =>
        await context
            .Statuses.AsNoTracking()
            .Where(s => s.Name == statusName)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync()
        ?? throw new InvalidOperationException($"The {statusName} status does not exist.");

    [GeneratedRegex(@"TK-(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TicketTagPattern();
}
