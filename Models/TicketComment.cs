namespace Safi_Ticket.Models;

/// <summary>
/// One entry in a ticket's conversation: an email from the requester (or anyone
/// copied on the thread), a reply emailed by staff, or an internal note.
/// </summary>
public class TicketComment
{
    public const string RequesterAuthor = "Requester";
    public const string AgentAuthor = "Agent";

    public int Id { get; set; }

    public int TicketId { get; set; }

    public Ticket Ticket { get; set; } = null!;

    public string Body { get; set; } = string.Empty;

    public string? AuthorName { get; set; }

    public string? AuthorEmail { get; set; }

    public string AuthorType { get; set; } = RequesterAuthor;

    public bool IsInternalNote { get; set; } = false;

    public int? UserId { get; set; }

    public User? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Everyone the email was addressed to. Empty for internal notes.</summary>
    public List<EmailParticipant> ToRecipients { get; set; } = [];

    /// <summary>Everyone copied on the email. Empty for internal notes.</summary>
    public List<EmailParticipant> CcRecipients { get; set; } = [];

    public bool IsFromRequesterSide =>
        string.Equals(AuthorType, RequesterAuthor, StringComparison.OrdinalIgnoreCase);
}
