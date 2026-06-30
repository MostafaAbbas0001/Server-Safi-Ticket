namespace Safi_Ticket.Models
{
    public class TicketComment
    {
        public int Id { get; set; }

        public int TicketId { get; set; }

        public Ticket Ticket { get; set; } = null!;

        public string Body { get; set; } = string.Empty;

        public string? AuthorName { get; set; }

        public string? AuthorEmail { get; set; }

        public string AuthorType { get; set; } = "Requester";

        public bool IsInternalNote { get; set; } = false;

        public int? UserId { get; set; }

        public User? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
