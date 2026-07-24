namespace Safi_Ticket.DTO.Request
{
    public class TicketQueryRequest
    {
        public int Page { get; set; } = 1;
        public int? StatusId { get; set; }
        public List<int> StatusIds { get; set; } = new();
        public int? UserId { get; set; }
        public string? Search { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
