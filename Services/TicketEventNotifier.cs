using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Safi_Ticket.Services
{
    public record TicketEvent(
        string Type,
        int TicketId,
        int? CommentId,
        string Message,
        DateTime CreatedAt
    );

    public class TicketEventNotifier
    {
        private readonly ConcurrentDictionary<Guid, Channel<TicketEvent>> _subscribers = new();

        public (Guid SubscriptionId, ChannelReader<TicketEvent> Reader) Subscribe()
        {
            var subscriptionId = Guid.NewGuid();
            var channel = Channel.CreateUnbounded<TicketEvent>();

            _subscribers[subscriptionId] = channel;

            return (subscriptionId, channel.Reader);
        }

        public void Unsubscribe(Guid subscriptionId)
        {
            if (_subscribers.TryRemove(subscriptionId, out var channel))
            {
                channel.Writer.TryComplete();
            }
        }

        public void Publish(TicketEvent ticketEvent)
        {
            foreach (var subscriber in _subscribers.ToArray())
            {
                if (!subscriber.Value.Writer.TryWrite(ticketEvent))
                {
                    Unsubscribe(subscriber.Key);
                }
            }
        }
    }
}
