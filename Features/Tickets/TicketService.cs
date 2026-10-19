using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit.Utils;
using Safi_Ticket.Common;
using Safi_Ticket.Common.Auth;
using Safi_Ticket.Common.Configuration;
using Safi_Ticket.Common.Storage;
using Safi_Ticket.Data;
using Safi_Ticket.Email;
using Safi_Ticket.Models;

namespace Safi_Ticket.Features.Tickets;

/// <summary>
/// Everything that changes a ticket. The acting staff member always comes from
/// their sign-in (<see cref="Actor"/>), never from the request body, and the
/// emails each change triggers go out on the background queue so the UI
/// doesn't wait for SMTP.
/// </summary>
public sealed class TicketService(
    ApplicationDbContext context,
    TicketQueries queries,
    AttachmentStorage storage,
    BackgroundEmailQueue emailQueue,
    IOptions<EmailSettings> emailSettings,
    ILogger<TicketService> logger
)
{
    /// <summary>
    /// Logs a ticket on someone's behalf. The description becomes the opening
    /// message of the thread, and everyone the request is for (requester, To
    /// and Cc) is emailed that IT logged it, with a reminder to email the
    /// helpdesk next time. The opening message records exactly who that email
    /// went to, so "reply all" later reaches the same people.
    /// </summary>
    public async Task<Ticket> CreateAsync(
        CreateTicketRequest request,
        Actor actor,
        IEnumerable<IFormFile>? files = null
    )
    {
        var title = request.Title.Trim();
        var body = request.Body.Trim();
        var requester = request.Requester.Trim();

        if (title.Length == 0 || body.Length == 0 || requester.Length == 0)
        {
            throw AppException.BadRequest(
                "A ticket needs a subject, a description and a requester."
            );
        }

        var settings = emailSettings.Value;
        var requesterEmail = NormalizeEmail(request.RequesterEmail);

        if (settings.IsHelpdeskAddress(requesterEmail))
        {
            throw AppException.BadRequest(
                "The requester can't be the helpdesk mailbox. Enter the person who asked for help."
            );
        }

        var author = await LoadUserAsync(actor.UserId);

        // The staff member logging the ticket already knows about it.
        var recipients = EmailRecipients.Clean(
            [new EmailParticipant(requester, requesterEmail), .. request.To],
            request.Cc,
            email =>
                settings.IsHelpdeskAddress(email)
                || string.Equals(email, author.Email, StringComparison.OrdinalIgnoreCase)
        );

        var now = DateTime.UtcNow;
        var ticket = new Ticket
        {
            Title = title,
            Body = body,
            Requester = requester,
            RequesterEmail = requesterEmail,
            StatusId = await GetStatusIdAsync(TicketStatuses.Initiated),
            CreatedAt = now,
        };
        var openingMessage = new TicketComment
        {
            Ticket = ticket,
            Body = body,
            AuthorName = author.Name,
            AuthorEmail = author.Email,
            AuthorType = TicketComment.AgentAuthor,
            IsInternalNote = false,
            UserId = author.Id,
            CreatedAt = now,
            ToRecipients = [.. recipients.To],
            CcRecipients = [.. recipients.Cc],
        };

        await using var transaction = await context.Database.BeginTransactionAsync();
        context.Tickets.Add(ticket);
        context.TicketComments.Add(openingMessage);
        await context.SaveChangesAsync();

        // Recorded like a received email, so a reply to the notice finds this
        // ticket even if the mail client drops the [TK-n] tag from the subject.
        EmailMessage? notice = null;
        if (!recipients.IsEmpty)
        {
            notice = new EmailMessage
            {
                TicketId = ticket.Id,
                TicketCommentId = openingMessage.Id,
                MessageId = MimeUtils.GenerateMessageId(MessageIdDomain(settings)),
                FromEmail = settings.Username,
                FromName = settings.FromName,
                Subject = TicketNotifications.TicketLoggedSubject(ticket),
                Body = body,
                ReceivedAt = now,
                ProcessedAt = now,
            };
            context.EmailMessages.Add(notice);
            await context.SaveChangesAsync();
        }

        await transaction.CommitAsync();

        if (files != null)
        {
            await SaveAttachmentsAsync(ticket.Id, files);
        }

        if (notice != null)
        {
            var messageId = notice.MessageId;
            QueueEmail(
                $"'ticket logged' email for ticket {ticket.Id}",
                notifications =>
                    notifications.SendTicketLoggedAsync(ticket, author.Name, recipients, messageId)
            );
        }

        return ticket;
    }

    public async Task<TicketAttachment> AddAttachmentAsync(int ticketId, IFormFile file)
    {
        await LoadTicketAsync(ticketId);
        var saved = await SaveAttachmentsAsync(ticketId, [file]);
        return saved.FirstOrDefault() ?? throw AppException.BadRequest("The file is empty.");
    }

    /// <summary>Edits the title/body, or (admins) reassigns. Assigning moves the ticket to In Progress.</summary>
    public async Task UpdateAsync(int ticketId, UpdateTicketRequest request, Actor actor)
    {
        var ticket = await LoadTicketAsync(ticketId);
        await EnsureNotFinishedAsync(ticket, "Closed and cancelled tickets can't be changed.");

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            ticket.Title = request.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            ticket.Body = request.Body.Trim();
        }

        User? newAssignee = null;

        if (request.UserId.HasValue && request.UserId != ticket.UserId)
        {
            if (!actor.IsAdmin)
            {
                throw AppException.Forbidden("Only administrators can reassign tickets.");
            }

            newAssignee =
                await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UserId)
                ?? throw AppException.BadRequest("That staff member doesn't exist.");

            ticket.UserId = newAssignee.Id;
            ticket.StatusId = await GetStatusIdAsync(TicketStatuses.InProgress);
        }

        await context.SaveChangesAsync();

        if (newAssignee != null)
        {
            QueueEmail(
                $"assignment email for ticket {ticket.Id}",
                notifications => notifications.SendTicketAssignedAsync(ticket, newAssignee)
            );
        }
    }

    public async Task<TicketCommentResponse> AddCommentAsync(
        int ticketId,
        CreateCommentRequest request,
        Actor actor
    )
    {
        await LoadTicketAsync(ticketId);
        var author = await LoadUserAsync(actor.UserId);

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw AppException.BadRequest("Write something before adding the note.");
        }

        var comment = new TicketComment
        {
            TicketId = ticketId,
            Body = request.Body.Trim(),
            AuthorName = author.Name,
            AuthorEmail = author.Email,
            AuthorType = TicketComment.AgentAuthor,
            IsInternalNote = request.IsInternalNote,
            UserId = author.Id,
            CreatedAt = DateTime.UtcNow,
        };

        context.TicketComments.Add(comment);
        await context.SaveChangesAsync();

        return TicketCommentResponse.From(comment);
    }

    /// <summary>
    /// Emails a reply. Without explicit recipients it goes to everyone on the
    /// thread (reply all). Officers can only reply on tickets assigned to them.
    /// </summary>
    public async Task<TicketCommentResponse> ReplyAsync(
        int ticketId,
        CreateReplyRequest request,
        Actor actor
    )
    {
        var ticket = await LoadTicketAsync(ticketId);

        if (!actor.IsAdmin && ticket.UserId != actor.UserId)
        {
            throw AppException.Forbidden("You can only reply on tickets assigned to you.");
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw AppException.BadRequest("Write your reply before sending it.");
        }

        var recipients =
            request.To == null
                ? await queries.ResolveReplyAllAsync(ticketId)
                : EmailRecipients.Clean(
                    request.To,
                    request.Cc ?? [],
                    emailSettings.Value.IsHelpdeskAddress
                );

        if (recipients.To.Count == 0)
        {
            throw AppException.BadRequest("Add at least one recipient for this reply.");
        }

        var author = await LoadUserAsync(actor.UserId);
        var reply = new TicketComment
        {
            TicketId = ticketId,
            Body = request.Body.Trim(),
            AuthorName = author.Name,
            AuthorEmail = author.Email,
            AuthorType = TicketComment.AgentAuthor,
            IsInternalNote = false,
            UserId = author.Id,
            CreatedAt = DateTime.UtcNow,
            ToRecipients = [.. recipients.To],
            CcRecipients = [.. recipients.Cc],
        };

        context.TicketComments.Add(reply);
        await context.SaveChangesAsync();

        var inReplyTo = await queries.GetLatestMessageIdAsync(ticketId);
        QueueEmail(
            $"reply email for ticket {ticketId}",
            notifications => notifications.SendTicketReplyAsync(ticket, reply, inReplyTo)
        );

        return TicketCommentResponse.From(reply);
    }

    /// <summary>Closes the ticket and emails everyone on the thread, with the optional resolution message.</summary>
    public async Task CloseAsync(int ticketId, CloseTicketRequest request, Actor actor)
    {
        var ticket = await LoadTicketAsync(ticketId);
        var statusName = await GetStatusNameAsync(ticket.StatusId);

        if (statusName == TicketStatuses.Cancelled)
        {
            throw AppException.BadRequest("Cancelled tickets can't be closed.");
        }

        if (statusName == TicketStatuses.Closed)
        {
            throw AppException.BadRequest("This ticket is already closed.");
        }

        var recipients = await queries.ResolveReplyAllAsync(ticketId);
        var author = await LoadUserAsync(actor.UserId);
        var message = request.Body.Trim();

        ticket.StatusId = await GetStatusIdAsync(TicketStatuses.Closed);

        if (message.Length > 0)
        {
            context.TicketComments.Add(
                new TicketComment
                {
                    TicketId = ticketId,
                    Body = message,
                    AuthorName = author.Name,
                    AuthorEmail = author.Email,
                    AuthorType = TicketComment.AgentAuthor,
                    IsInternalNote = false,
                    UserId = author.Id,
                    CreatedAt = DateTime.UtcNow,
                    ToRecipients = [.. recipients.To],
                    CcRecipients = [.. recipients.Cc],
                }
            );
        }

        await context.SaveChangesAsync();

        var inReplyTo = await queries.GetLatestMessageIdAsync(ticketId);
        QueueEmail(
            $"closed email for ticket {ticketId}",
            notifications =>
                notifications.SendTicketClosedAsync(
                    ticket,
                    message,
                    author.Name,
                    recipients,
                    inReplyTo
                )
        );
    }

    public async Task CancelAsync(int ticketId)
    {
        var ticket = await LoadTicketAsync(ticketId);

        if (await GetStatusNameAsync(ticket.StatusId) == TicketStatuses.Closed)
        {
            throw AppException.BadRequest("Closed tickets can't be cancelled.");
        }

        ticket.StatusId = await GetStatusIdAsync(TicketStatuses.Cancelled);
        await context.SaveChangesAsync();
    }

    /// <summary>Records that the viewer has seen every requester email so far.</summary>
    public async Task MarkRequesterRepliesAsReadAsync(int ticketId, int userId)
    {
        var replyVersion =
            await context
                .Tickets.AsNoTracking()
                .NotDeleted()
                .Where(ticket => ticket.Id == ticketId)
                .Select(ticket => (long?)ticket.RequesterReplyVersion)
                .FirstOrDefaultAsync()
            ?? throw TicketQueries.TicketNotFound(ticketId);

        var readState = await context.TicketReplyReadStates.FindAsync(userId, ticketId);

        if (readState == null)
        {
            context.TicketReplyReadStates.Add(
                new TicketReplyReadState
                {
                    UserId = userId,
                    TicketId = ticketId,
                    SeenRequesterReplyVersion = replyVersion,
                    LastReadAt = DateTime.UtcNow,
                }
            );
        }
        else
        {
            readState.SeenRequesterReplyVersion = Math.Max(
                readState.SeenRequesterReplyVersion,
                replyVersion
            );
            readState.LastReadAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    private async Task<List<TicketAttachment>> SaveAttachmentsAsync(
        int ticketId,
        IEnumerable<IFormFile> files
    )
    {
        var saved = new List<TicketAttachment>();

        foreach (var file in files.Where(file => file.Length > 0))
        {
            saved.Add(
                await storage.SaveAsync(
                    ticketId,
                    file.FileName,
                    file.ContentType,
                    stream => file.CopyToAsync(stream)
                )
            );
        }

        if (saved.Count > 0)
        {
            context.TicketAttachments.AddRange(saved);
            await context.SaveChangesAsync();
        }

        return saved;
    }

    /// <summary>
    /// Sends an email after the response returns. The work captures plain
    /// objects only; the queue gives it its own service scope.
    /// </summary>
    private void QueueEmail(string description, Func<TicketNotifications, Task> send)
    {
        emailQueue.Enqueue(
            async (services, _) =>
            {
                try
                {
                    await send(services.GetRequiredService<TicketNotifications>());
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Couldn't send the {Description}.", description);
                }
            }
        );
    }

    private async Task<Ticket> LoadTicketAsync(int ticketId) =>
        await context.Tickets.NotDeleted().FirstOrDefaultAsync(ticket => ticket.Id == ticketId)
        ?? throw TicketQueries.TicketNotFound(ticketId);

    private async Task<User> LoadUserAsync(int userId) =>
        await context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == userId)
        ?? throw AppException.Unauthorized("Your account no longer exists. Sign in again.");

    private async Task EnsureNotFinishedAsync(Ticket ticket, string message)
    {
        if (TicketStatuses.IsFinished(await GetStatusNameAsync(ticket.StatusId)))
        {
            throw AppException.BadRequest(message);
        }
    }

    private Task<string?> GetStatusNameAsync(int statusId) =>
        context
            .Statuses.AsNoTracking()
            .Where(s => s.Id == statusId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync();

    private async Task<int> GetStatusIdAsync(string statusName) =>
        await context
            .Statuses.AsNoTracking()
            .Where(s => s.Name == statusName)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync()
        ?? throw new InvalidOperationException($"The {statusName} status does not exist.");

    /// <summary>Message-Ids end in the helpdesk's own domain, as its mail server's would.</summary>
    private static string MessageIdDomain(EmailSettings settings)
    {
        var at = settings.Username.LastIndexOf('@');
        return at >= 0 && at < settings.Username.Length - 1
            ? settings.Username[(at + 1)..]
            : "safi-ticket.local";
    }

    private static string NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw AppException.BadRequest("Enter the requester's email so replies reach them.");
        }

        try
        {
            return new MailAddress(email.Trim()).Address;
        }
        catch (FormatException)
        {
            throw AppException.BadRequest("That requester email doesn't look right.");
        }
    }
}
