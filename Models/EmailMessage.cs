namespace Safi_Ticket.Models
{
    public class EmailMessage
    {
        public int Id { get; set; }

        public int TicketId { get; set; }

        public Ticket Ticket { get; set; } = null!;

        public int? TicketCommentId { get; set; }

        public TicketComment? TicketComment { get; set; }

        public string MessageId { get; set; } = string.Empty;

        public string FromEmail { get; set; } = string.Empty;

        public string? FromName { get; set; }

        public string? Subject { get; set; }

        public string Body { get; set; } = string.Empty;

        public DateTime ReceivedAt { get; set; }

        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }
}
