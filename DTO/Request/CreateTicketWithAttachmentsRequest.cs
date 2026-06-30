using Microsoft.AspNetCore.Http;
using Safi_Ticket.Models;

namespace Safi_Ticket.DTO.Request
{
    public class CreateTicketWithAttachmentsRequest : CreateTicketRequest
    {
        public List<IFormFile> Attachments { get; set; } = new();
    }
}
