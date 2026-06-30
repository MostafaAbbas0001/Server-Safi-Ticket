namespace Safi_Ticket.DTO.Settings
{
    public class EmailSettings
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Mailbox { get; set; } = "INBOX";

        public int PollSeconds { get; set; } = 300;

        public string SmtpHost { get; set; } = string.Empty;

        public int SmtpPort { get; set; } = 465;

        public string FromName { get; set; } = "Safi Helpdesk";
    }
}
