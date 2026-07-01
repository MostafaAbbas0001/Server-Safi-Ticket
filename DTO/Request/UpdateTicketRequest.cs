namespace Safi_Ticket.DTO.Request
{
    public class UpdateTicketRequest
    {
        private int? _userId;

        public string? Title { get; set; }

        public string? Body { get; set; }

        public int? StatusId { get; set; }

        public int? UserId
        {
            get => _userId;
            set
            {
                _userId = value;
                IsUserIdSet = true;
            }
        }

        public bool IsUserIdSet { get; private set; }

        public int? PriorityId { get; set; }

        public int? ActorUserId { get; set; }

        public string? InternalNote { get; set; }
    }
}
