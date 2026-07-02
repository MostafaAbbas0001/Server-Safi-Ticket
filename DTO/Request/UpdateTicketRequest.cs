namespace Safi_Ticket.DTO.Request
{
    public class UpdateTicketRequest
    {
        public string? Title { get; set; }
        public string? Body { get; set; }
        public int? UserId { get; set; }
    }
}
