using System.ComponentModel.DataAnnotations;

namespace Safi_Ticket.DTO.Request
{
    public class TicketReplyRequest
    {
        public int UserId { get; set; }

        [Required]
        public string Body { get; set; } = string.Empty;
    }
}