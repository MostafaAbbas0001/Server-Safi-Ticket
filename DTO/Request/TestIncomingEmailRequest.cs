namespace Safi_Ticket.DTO.Request
{
    public class TestIncomingEmailRequest
    {
        public string MessageId { get; set; } = string.Empty;

        public string FromEmail { get; set; } = string.Empty;

        public string? FromName { get; set; }

        public string Subject { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    }
}
