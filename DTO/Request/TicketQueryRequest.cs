namespace Safi_Ticket.DTO.Request
{
    public class TicketQueryRequest
    {
        public int Page { get; set; } = 1;

        public int? StatusId { get; set; }

        public int? UserId { get; set; }

        public string? Search { get; set; }
    }
}
