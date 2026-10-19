using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Safi_Ticket.Common;
using Safi_Ticket.Common.Configuration;
using Safi_Ticket.Data;
using Safi_Ticket.Email;
using Safi_Ticket.Models;

namespace Safi_Ticket.Features.Tickets;

/// <summary>Everything that reads tickets: the list, one ticket, its conversation and files.</summary>
public sealed class TicketQueries(
    ApplicationDbContext context,
    IOptions<EmailSettings> emailSettings
)
{
    public const int PageSize = 50;

    public async Task<TicketSearchResponse> SearchAsync(
        TicketQueryRequest request,
        int viewerUserId
    )
    {
        var page = Math.Max(1, request.Page);
        var statusIds = request
            .StatusIds.Append(request.StatusId ?? 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var tickets = context
            .Tickets.AsNoTracking()
            .NotDeleted()
            .AssignedTo(request.UserId)
            .CreatedBetween(request.StartDate, request.EndDate);

        if (statusIds.Count > 0)
        {
            tickets = tickets.Where(ticket => statusIds.Contains(ticket.StatusId));
        }

        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            var hasTicketId = int.TryParse(
                search
                    .Replace("TK-", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .TrimStart('#'),
                out var ticketId
            );

            tickets = tickets.Where(ticket =>
                EF.Functions.ILike(ticket.Title, pattern)
                || EF.Functions.ILike(ticket.Body, pattern)
                || EF.Functions.ILike(ticket.Requester, pattern)
                || (
                    ticket.RequesterEmail != null
                    && EF.Functions.ILike(ticket.RequesterEmail, pattern)
                )
                || (hasTicketId && ticket.Id == ticketId)
            );
        }

        var totalCount = await tickets.CountAsync();
        var items = await tickets
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ThenByDescending(ticket => ticket.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(ToListItem(viewerUserId))
            .ToListAsync();

        return new TicketSearchResponse(items, page, PageSize, totalCount);
    }

    public async Task<TicketListItemResponse> GetListItemAsync(int ticketId, int viewerUserId) =>
        await context
            .Tickets.AsNoTracking()
            .NotDeleted()
            .Where(ticket => ticket.Id == ticketId)
            .Select(ToListItem(viewerUserId))
            .FirstOrDefaultAsync()
        ?? throw TicketNotFound(ticketId);

    public async Task<TicketDetailResponse> GetDetailAsync(int ticketId, int viewerUserId)
    {
        var item = await GetListItemAsync(ticketId, viewerUserId);
        var recipients = await ResolveReplyAllAsync(ticketId);

        return new TicketDetailResponse
        {
            Id = item.Id,
            Title = item.Title,
            Body = item.Body,
            Requester = item.Requester,
            RequesterEmail = item.RequesterEmail,
            StatusId = item.StatusId,
            Status = item.Status,
            UserId = item.UserId,
            Assignee = item.Assignee,
            CreatedAt = item.CreatedAt,
            CommentCount = item.CommentCount,
            LastCommentAt = item.LastCommentAt,
            HasUnreadRequesterReply = item.HasUnreadRequesterReply,
            UnreadRequesterReplyCount = item.UnreadRequesterReplyCount,
            LastRequesterReplyAt = item.LastRequesterReplyAt,
            ReplyRecipients = new ReplyRecipientsResponse(recipients.To, recipients.Cc),
        };
    }

    /// <summary>"Reply all" for the ticket: see <see cref="EmailRecipients.ResolveReplyAll"/>.</summary>
    public async Task<ReplyRecipients> ResolveReplyAllAsync(int ticketId)
    {
        var ticket =
            await context
                .Tickets.AsNoTracking()
                .NotDeleted()
                .FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw TicketNotFound(ticketId);
        var emails = await context
            .TicketComments.AsNoTracking()
            .Where(comment => comment.TicketId == ticketId && !comment.IsInternalNote)
            .ToListAsync();

        return EmailRecipients.ResolveReplyAll(
            ticket,
            emails,
            emailSettings.Value.IsHelpdeskAddress
        );
    }

    /// <summary>
    /// Message-Id of the latest email on the ticket's thread (a received email, or
    /// the notice sent when the ticket was logged by hand), for threading replies.
    /// </summary>
    public Task<string?> GetLatestMessageIdAsync(int ticketId) =>
        context
            .EmailMessages.AsNoTracking()
            .Where(email => email.TicketId == ticketId && email.MessageId.Contains("@"))
            .OrderByDescending(email => email.ReceivedAt)
            .Select(email => email.MessageId)
            .FirstOrDefaultAsync();

    public async Task<List<TicketCommentResponse>> GetCommentsAsync(int ticketId)
    {
        var comments = await context
            .TicketComments.AsNoTracking()
            .Where(comment => comment.TicketId == ticketId)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .ToListAsync();

        return comments.Select(TicketCommentResponse.From).ToList();
    }

    public async Task<List<TicketAttachmentResponse>> GetAttachmentsAsync(int ticketId)
    {
        var attachments = await context
            .TicketAttachments.AsNoTracking()
            .Where(attachment => attachment.TicketId == ticketId)
            .OrderBy(attachment => attachment.UploadedAt)
            .ToListAsync();

        return attachments.Select(TicketAttachmentResponse.From).ToList();
    }

    public async Task<TicketAttachment> GetAttachmentAsync(int attachmentId) =>
        await context
            .TicketAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId)
        ?? throw AppException.NotFound($"Attachment {attachmentId} doesn't exist.");

    public static AppException TicketNotFound(int ticketId) =>
        AppException.NotFound($"Ticket #{ticketId} doesn't exist.");

    /// <summary>
    /// One projection for both the list and a single ticket. Unread counts are
    /// per viewer: how many requester emails arrived since they last opened it.
    /// </summary>
    private static Expression<Func<Ticket, TicketListItemResponse>> ToListItem(int viewerUserId) =>
        ticket => new TicketListItemResponse
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Body = ticket.Body,
            Requester = ticket.Requester,
            RequesterEmail = ticket.RequesterEmail,
            StatusId = ticket.StatusId,
            Status = ticket.Status.Name,
            UserId = ticket.UserId,
            Assignee = ticket.User != null ? ticket.User.Name : null,
            CreatedAt = ticket.CreatedAt,
            CommentCount = ticket.Comments.Count,
            LastCommentAt = ticket.Comments.Max(comment => (DateTime?)comment.CreatedAt),
            HasUnreadRequesterReply =
                ticket.RequesterReplyVersion
                > ticket
                    .ReplyReadStates.Where(state => state.UserId == viewerUserId)
                    .Select(state => state.SeenRequesterReplyVersion)
                    .FirstOrDefault(),
            UnreadRequesterReplyCount =
                ticket.RequesterReplyVersion
                - ticket
                    .ReplyReadStates.Where(state => state.UserId == viewerUserId)
                    .Select(state => state.SeenRequesterReplyVersion)
                    .FirstOrDefault(),
            LastRequesterReplyAt = ticket.LastRequesterReplyAt,
        };
}
