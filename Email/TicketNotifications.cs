using Microsoft.Extensions.Options;
using Safi_Ticket.Common.Configuration;
using Safi_Ticket.Models;

namespace Safi_Ticket.Email;

/// <summary>
/// The emails the helpdesk sends: what each one says, and who receives it.
/// Ticket bodies may be HTML (emails with tables), so they always go through
/// <see cref="EmailHtml"/> - sanitized HTML in the HTML part, readable text in
/// the plain part - instead of being escaped into visible tags.
/// </summary>
public sealed class TicketNotifications(
    EmailSender sender,
    IOptions<FrontendSettings> frontend,
    IOptions<EmailSettings> emailSettings
)
{
    private readonly FrontendSettings _frontend = frontend.Value;
    private readonly EmailSettings _email = emailSettings.Value;

    private static string Tag(Ticket ticket) => $"TK-{ticket.Id}";

    private static string ReplyFootnote(Ticket ticket) =>
        $"Reply to this email to continue the conversation. Keep [{Tag(ticket)}] in the subject so your message reaches this ticket.";

    /// <summary>Acknowledges a new emailed request to the person who sent it.</summary>
    public Task SendTicketReceivedAsync(Ticket ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket.RequesterEmail))
        {
            return Task.CompletedTask;
        }

        var content = new EmailContent
        {
            Preheader = $"We've logged your request as {Tag(ticket)}.",
            Ticket = ticket,
            StatusName = TicketStatuses.Initiated,
            Greeting = $"Hello {ticket.Requester},",
            Intro =
                "We've received your request and logged it as a ticket. Someone from IT will pick it up and reply here.",
            MessageLabel = "Your request",
            MessageHtml = EmailHtml.ToEmailHtml(ticket.Body),
            Footnote = ReplyFootnote(ticket),
        };

        return sender.SendAsync(
            new OutgoingEmail
            {
                To = [new EmailParticipant(ticket.Requester, ticket.RequesterEmail)],
                Subject = $"[{Tag(ticket)}] Request received: {ticket.Title}",
                TextBody = PlainText(
                    content.Greeting,
                    content.Intro,
                    $"Ticket: {Tag(ticket)} - {ticket.Title}",
                    "Your request:",
                    EmailHtml.ToPlainText(ticket.Body),
                    content.Footnote
                ),
                HtmlBody = EmailTemplates.Render(content),
            }
        );
    }

    public static string TicketLoggedSubject(Ticket ticket) =>
        $"[{Tag(ticket)}] We've logged your request: {ticket.Title}";

    /// <summary>
    /// Tells the people a hand-logged ticket is for that IT logged it, and asks
    /// them to email the helpdesk next time rather than contacting IT directly.
    /// </summary>
    public Task SendTicketLoggedAsync(
        Ticket ticket,
        string loggedBy,
        ReplyRecipients recipients,
        string messageId
    )
    {
        var helpdesk = _email.Username;
        var greetsRequester = recipients.To.Any(person =>
            string.Equals(person.Email, ticket.RequesterEmail, StringComparison.OrdinalIgnoreCase)
        );
        var callout = new EmailCallout(
            "Next time, email your request to",
            helpdesk,
            "It opens a ticket automatically, so whoever is free in IT can pick it up straight away instead of it waiting on one person."
        );
        var content = new EmailContent
        {
            Preheader =
                $"{loggedBy} logged your request as {Tag(ticket)}. Next time, email {helpdesk}.",
            Ticket = ticket,
            StatusName = TicketStatuses.Initiated,
            Greeting = greetsRequester ? $"Hello {ticket.Requester}," : "Hello,",
            Intro =
                $"{loggedBy} from IT has logged your request as ticket {Tag(ticket)}, so it's tracked until it's done. You'll get updates by email as it moves along.",
            Callout = callout,
            MessageLabel = "Your request",
            // Typed in the app as plain text - never parse it as HTML.
            MessageHtml = EmailHtml.FromPlainText(ticket.Body),
            Footnote = ReplyFootnote(ticket),
        };

        return sender.SendAsync(
            new OutgoingEmail
            {
                To = recipients.To,
                Cc = recipients.Cc,
                Subject = TicketLoggedSubject(ticket),
                TextBody = PlainText(
                    content.Greeting,
                    content.Intro,
                    string.IsNullOrWhiteSpace(helpdesk)
                        ? null
                        : $"{callout.Lead} {helpdesk}. {callout.Text}",
                    $"Ticket: {Tag(ticket)} - {ticket.Title}",
                    "Your request:",
                    ticket.Body.Trim(),
                    content.Footnote
                ),
                HtmlBody = EmailTemplates.Render(content),
                MessageId = messageId,
            }
        );
    }

    /// <summary>Tells a staff member a ticket is now theirs, with a link straight to it.</summary>
    public Task SendTicketAssignedAsync(Ticket ticket, User assignee)
    {
        if (string.IsNullOrWhiteSpace(assignee.Email))
        {
            return Task.CompletedTask;
        }

        var ticketUrl = BuildTicketUrl(ticket.Id);
        var content = new EmailContent
        {
            Preheader = $"{Tag(ticket)} from {ticket.Requester} is now yours.",
            Ticket = ticket,
            StatusName = TicketStatuses.InProgress,
            Greeting = $"Hello {assignee.Name},",
            Intro = "This ticket has been assigned to you.",
            MessageLabel = "Request",
            MessageHtml = EmailHtml.ToEmailHtml(ticket.Body),
            Details =
            [
                ("Requester", ticket.Requester),
                ("Email", ticket.RequesterEmail ?? "Not provided"),
            ],
            ActionLabel = ticketUrl != null ? "Open ticket" : null,
            ActionUrl = ticketUrl,
            Footnote = "You're receiving this because you're now the assignee.",
        };

        return sender.SendAsync(
            new OutgoingEmail
            {
                To = [new EmailParticipant(assignee.Name, assignee.Email)],
                Subject = $"[{Tag(ticket)}] Assigned to you: {ticket.Title}",
                TextBody = PlainText(
                    content.Greeting,
                    content.Intro,
                    $"Ticket: {Tag(ticket)} - {ticket.Title}",
                    $"Requester: {ticket.Requester} ({ticket.RequesterEmail})",
                    "Request:",
                    EmailHtml.ToPlainText(ticket.Body),
                    ticketUrl != null ? $"Open it here: {ticketUrl}" : null
                ),
                HtmlBody = EmailTemplates.Render(content),
            }
        );
    }

    /// <summary>Emails a staff reply to everyone it was addressed to.</summary>
    public Task SendTicketReplyAsync(Ticket ticket, TicketComment reply, string? inReplyTo)
    {
        var author = reply.AuthorName ?? "Safi IT";
        var content = new EmailContent
        {
            Preheader = reply.Body.Trim() is { Length: > 0 } text
                ? text[..Math.Min(text.Length, 120)]
                : $"New reply on {Tag(ticket)}.",
            Ticket = ticket,
            MessageAuthor = author,
            // Replies are typed in the app as plain text - never parse them as HTML.
            MessageHtml = EmailHtml.FromPlainText(reply.Body),
            Footnote = ReplyFootnote(ticket),
        };

        return sender.SendAsync(
            new OutgoingEmail
            {
                To = reply.ToRecipients,
                Cc = reply.CcRecipients,
                Subject = $"Re: [{Tag(ticket)}] {ticket.Title}",
                TextBody = PlainText(
                    reply.Body.Trim(),
                    $"{author}, Safi IT Department",
                    content.Footnote
                ),
                HtmlBody = EmailTemplates.Render(content),
                InReplyTo = inReplyTo,
            }
        );
    }

    /// <summary>Tells everyone on the thread the ticket is closed, with the optional resolution note.</summary>
    public Task SendTicketClosedAsync(
        Ticket ticket,
        string? closingMessage,
        string? closedBy,
        ReplyRecipients recipients,
        string? inReplyTo
    )
    {
        var message = closingMessage?.Trim();
        var hasMessage = !string.IsNullOrWhiteSpace(message);
        var content = new EmailContent
        {
            Preheader = $"{Tag(ticket)} has been resolved and closed.",
            Ticket = ticket,
            StatusName = TicketStatuses.Closed,
            Greeting = $"Hello {ticket.Requester},",
            Intro = hasMessage
                ? "Your ticket has been resolved and closed."
                : "Your ticket has been resolved and closed. No further action is needed.",
            MessageAuthor = hasMessage ? closedBy ?? "Safi IT" : null,
            MessageHtml = hasMessage ? EmailHtml.FromPlainText(message) : null,
            Footnote =
                $"If the problem comes back, reply to this email and [{Tag(ticket)}] will reopen.",
        };

        return sender.SendAsync(
            new OutgoingEmail
            {
                To = recipients.To,
                Cc = recipients.Cc,
                Subject = $"Closed: [{Tag(ticket)}] {ticket.Title}",
                TextBody = PlainText(content.Greeting, content.Intro, message, content.Footnote),
                HtmlBody = EmailTemplates.Render(content),
                InReplyTo = inReplyTo,
            }
        );
    }

    public Task SendPasswordResetCodeAsync(User user, string resetCode)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return Task.CompletedTask;
        }

        var content = new EmailContent
        {
            Preheader = $"Your reset code is {resetCode}. It expires in 10 minutes.",
            Heading = "Reset your password",
            Greeting = $"Hello {user.Name},",
            Intro =
                "Enter this code on the Safi Ticket sign-in page to choose a new password. It expires in 10 minutes.",
            Code = resetCode,
            Footnote =
                "If you didn't ask to reset your password, you can ignore this email. Your password won't change.",
        };

        return sender.SendAsync(
            new OutgoingEmail
            {
                To = [new EmailParticipant(user.Name, user.Email)],
                Subject = "Your Safi Ticket password reset code",
                TextBody = PlainText(
                    content.Greeting,
                    $"Your reset code is {resetCode}. It expires in 10 minutes.",
                    content.Footnote
                ),
                HtmlBody = EmailTemplates.Render(content),
            }
        );
    }

    private string? BuildTicketUrl(int ticketId) =>
        string.IsNullOrWhiteSpace(_frontend.BaseUrl)
            ? null
            : $"{_frontend.BaseUrl.TrimEnd('/')}/tickets/{ticketId}";

    private static string PlainText(params string?[] paragraphs) =>
        string.Join("\n\n", paragraphs.Where(p => !string.IsNullOrWhiteSpace(p)))
        + "\n\nSafi IT Department";
}
