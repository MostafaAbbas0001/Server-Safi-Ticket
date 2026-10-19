namespace Safi_Ticket.Models;

public class TicketReplyReadState
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public long SeenRequesterReplyVersion { get; set; }
    public DateTime LastReadAt { get; set; } = DateTime.UtcNow;
}
