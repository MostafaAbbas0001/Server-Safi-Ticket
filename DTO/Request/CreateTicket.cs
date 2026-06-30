using System.ComponentModel.DataAnnotations;

namespace Safi_Ticket.Models
{
    public class CreateTicketRequest
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        [Required]
        public string Requester { get; set; } = string.Empty;

    }
}
