namespace Safi_Ticket.Models;

public class TicketAttachment
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    public Ticket Ticket { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Content-Id of an image embedded in an email body (referenced there as "cid:...").</summary>
    public string? ContentId { get; set; }

    /// <summary>True for images shown inside an email body rather than attached as files.</summary>
    public bool IsInline { get; set; }
}
