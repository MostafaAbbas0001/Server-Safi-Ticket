namespace Safi_Ticket.DTO.Response
{
    public class TicketSearchResponse
    {
        public List<TicketListItemResponse> Items { get; set; } = new();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }
    }
}
