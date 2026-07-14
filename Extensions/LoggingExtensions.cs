namespace Safi_Ticket.Extensions
{
    public static class LoggingExtensions
    {
        public static ILoggingBuilder AddApplicationLogging(this ILoggingBuilder logging)
        {
            logging.ClearProviders();
            logging.AddConsole();

            return logging;
        }
    }
}
