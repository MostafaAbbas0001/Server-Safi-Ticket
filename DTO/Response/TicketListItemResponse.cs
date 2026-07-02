namespace Safi_Ticket.DTO.Response
{
    public class TicketListItemResponse
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        public string Requester { get; set; } = string.Empty;

        public string? RequesterEmail { get; set; }

        public int StatusId { get; set; }

        public string? Status { get; set; }

        public int? UserId { get; set; }

        public string? Assignee { get; set; }

        public DateTime CreatedAt { get; set; }

        public int CommentCount { get; set; }

        public DateTime? LastCommentAt { get; set; }
    }
}
