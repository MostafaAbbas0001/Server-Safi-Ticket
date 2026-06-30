namespace Safi_Ticket.DTO.Response
{
    public class TicketSearchResponse
    {
        public List<TicketListItemResponse> Items { get; set; } = new();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int AllCount { get; set; }

        public int ActiveCount { get; set; }

        public int PendingCount { get; set; }

        public int ResolvedCount { get; set; }

        public int ClosedCount { get; set; }

        public Dictionary<int, int> StatusCounts { get; set; } = new();
    }
}
