using Microsoft.AspNetCore.Mvc;
using Safi_Ticket.Services;
using System.Text.Json;

namespace Safi_Ticket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly TicketEventNotifier _notifier;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public NotificationsController(TicketEventNotifier notifier)
        {
            _notifier = notifier;
        }

        [HttpGet("stream")]
        public async Task Stream(CancellationToken cancellationToken)
        {
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("X-Accel-Buffering", "no");

            var (subscriptionId, reader) = _notifier.Subscribe();

            try
            {
                await Response.WriteAsync(": connected\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);

                await foreach (var ticketEvent in reader.ReadAllAsync(cancellationToken))
                {
                    var json = JsonSerializer.Serialize(ticketEvent, _jsonOptions);

                    await Response.WriteAsync($"event: {ticketEvent.Type}\n", cancellationToken);
                    await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Browser closed the live notification stream.
            }
            finally
            {
                _notifier.Unsubscribe(subscriptionId);
            }
        }
    }
}
