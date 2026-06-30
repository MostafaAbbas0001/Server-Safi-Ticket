namespace Safi_Ticket.DTO.Request
{
    public class UserTicketUpdateRequest
    {
        public int UserId { get; set; }

        public int? StatusId { get; set; }

        public int? PriorityId { get; set; }

        public string? InternalNote { get; set; }
    }
}
