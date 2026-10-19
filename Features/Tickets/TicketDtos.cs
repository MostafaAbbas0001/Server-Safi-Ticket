using System.ComponentModel.DataAnnotations;
using Safi_Ticket.Models;

namespace Safi_Ticket.Features.Tickets;

// ---- Requests ------------------------------------------------------------

public class TicketQueryRequest
{
    public int Page { get; set; } = 1;
    public int? StatusId { get; set; }
    public List<int> StatusIds { get; set; } = [];
    public int? UserId { get; set; }
    public string? Search { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class CreateTicketRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    [Required]
    public string Requester { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string RequesterEmail { get; set; } = string.Empty;

    /// <summary>Anyone else the request is for. They're emailed alongside the requester.</summary>
    public List<EmailParticipant> To { get; set; } = [];

    /// <summary>People to keep informed (copied on the notice and on replies).</summary>
    public List<EmailParticipant> Cc { get; set; } = [];
}

public class CreateTicketWithAttachmentsRequest : CreateTicketRequest
{
    public List<IFormFile> Attachments { get; set; } = [];
}

public class UpdateTicketRequest
{
    public string? Title { get; set; }
    public string? Body { get; set; }

    /// <summary>New assignee. Admins only.</summary>
    public int? UserId { get; set; }
}

/// <summary>An internal note (or, with <see cref="IsInternalNote"/> false, an unsent comment).</summary>
public class CreateCommentRequest
{
    public string Body { get; set; } = string.Empty;
    public bool IsInternalNote { get; set; } = true;
}

public class CreateReplyRequest
{
    public string Body { get; set; } = string.Empty;

    /// <summary>Who to email. Leave null to reply to everyone on the thread.</summary>
    public List<EmailParticipant>? To { get; set; }

    public List<EmailParticipant>? Cc { get; set; }
}

public class CloseTicketRequest
{
    /// <summary>Optional resolution message, emailed to everyone on the thread.</summary>
    public string Body { get; set; } = string.Empty;
}

// ---- Responses -----------------------------------------------------------

public class TicketListItemResponse
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string Requester { get; init; } = string.Empty;
    public string? RequesterEmail { get; init; }
    public int StatusId { get; init; }
    public string? Status { get; init; }
    public int? UserId { get; init; }
    public string? Assignee { get; init; }
    public DateTime CreatedAt { get; init; }
    public int CommentCount { get; init; }
    public DateTime? LastCommentAt { get; init; }
    public bool HasUnreadRequesterReply { get; init; }
    public long UnreadRequesterReplyCount { get; init; }
    public DateTime? LastRequesterReplyAt { get; init; }
}

public sealed class TicketDetailResponse : TicketListItemResponse
{
    /// <summary>Who a reply goes to by default ("reply all" on the latest email).</summary>
    public required ReplyRecipientsResponse ReplyRecipients { get; init; }
}

public sealed record ReplyRecipientsResponse(
    IReadOnlyList<EmailParticipant> To,
    IReadOnlyList<EmailParticipant> Cc
);

public sealed record TicketSearchResponse(
    List<TicketListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount
);

public sealed record TicketCommentResponse(
    int Id,
    int TicketId,
    string Body,
    string? AuthorName,
    string? AuthorEmail,
    string AuthorType,
    bool IsInternalNote,
    int? UserId,
    DateTime CreatedAt,
    IReadOnlyList<EmailParticipant> To,
    IReadOnlyList<EmailParticipant> Cc
)
{
    public static TicketCommentResponse From(TicketComment comment) =>
        new(
            comment.Id,
            comment.TicketId,
            comment.Body,
            comment.AuthorName,
            comment.AuthorEmail,
            comment.AuthorType,
            comment.IsInternalNote,
            comment.UserId,
            comment.CreatedAt,
            comment.ToRecipients,
            comment.CcRecipients
        );
}

public sealed record TicketAttachmentResponse(
    int Id,
    int TicketId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Url,
    string DownloadUrl,
    DateTime UploadedAt,
    string? ContentId,
    bool IsInline
)
{
    public static TicketAttachmentResponse From(TicketAttachment attachment) =>
        new(
            attachment.Id,
            attachment.TicketId,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.Url,
            $"/api/ticket/attachments/{attachment.Id}/file",
            attachment.UploadedAt,
            attachment.ContentId,
            attachment.IsInline
        );
}

public sealed record CreatedTicketResponse(
    int Id,
    string Title,
    string Body,
    string Requester,
    string? RequesterEmail,
    int StatusId,
    int? UserId,
    DateTime CreatedAt,
    IReadOnlyList<TicketAttachmentResponse> Attachments
);
