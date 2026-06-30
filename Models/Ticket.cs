namespace Safi_Ticket.Models
{
    public class Ticket
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int StatusId { get; set; }

        public Status Status { get; set; } = null!;

        public string Requester { get; set; } = string.Empty;

        public string? RequesterEmail { get; set; }

        public int? PriorityId { get; set; }

        public Priority? Priority { get; set; }

        public int? UserId { get; set; }

        public User? User { get; set; }

        public bool IsDeleted { get; set; } = false;

        public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();

        public ICollection<EmailMessage> EmailMessages { get; set; } = new List<EmailMessage>();

        public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
    }
}
