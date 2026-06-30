namespace Safi_Ticket.DTO.Request
{
    public class TicketQueryRequest
    {
        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 50;

        public int? StatusId { get; set; }

        public int? PriorityId { get; set; }

        public string? Search { get; set; }
    }
}
