namespace Safi_Ticket.DTO.Request
{
    public class CreateTicketCommentRequest
    {
        public string Body { get; set; } = string.Empty;

        public string? AuthorName { get; set; }

        public string? AuthorEmail { get; set; }

        public string AuthorType { get; set; } = "Agent";

        public bool IsInternalNote { get; set; } = false;

        public int? UserId { get; set; }
    }
}
