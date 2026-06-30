namespace Safi_Ticket.Models
{
    public class Priority
    {
        public int Id { get; set; }

        public string Type { get; set; } = string.Empty;

        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}
