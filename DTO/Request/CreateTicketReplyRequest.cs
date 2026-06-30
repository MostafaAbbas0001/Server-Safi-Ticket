namespace Safi_Ticket.DTO.Request
{
    public class CreateTicketReplyRequest
    {
        public string Body { get; set; } = string.Empty;

        public string? AuthorName { get; set; }

        public string? AuthorEmail { get; set; }

        public int? UserId { get; set; }
    }
}
