using System.Net.Mail;
using MimeKit;
using Safi_Ticket.Models;

namespace Safi_Ticket.Email;

/// <summary>Who a reply goes to: the person being answered, and everyone else on the thread.</summary>
public sealed record ReplyRecipients(
    IReadOnlyList<EmailParticipant> To,
    IReadOnlyList<EmailParticipant> Cc
)
{
    public bool IsEmpty => To.Count == 0 && Cc.Count == 0;
}

public static class EmailRecipients
{
    /// <summary>Every mailbox in a MIME address list, with group addresses expanded.</summary>
    public static List<EmailParticipant> FromMime(InternetAddressList addresses) =>
        Normalize(
            addresses.Mailboxes.Select(mailbox => new EmailParticipant(
                string.IsNullOrWhiteSpace(mailbox.Name) ? null : mailbox.Name.Trim(),
                mailbox.Address
            ))
        );

    /// <summary>
    /// Trims, validates and de-duplicates addresses (case-insensitively), keeping
    /// the first name seen for each. Invalid addresses are dropped.
    /// </summary>
    public static List<EmailParticipant> Normalize(IEnumerable<EmailParticipant> participants)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<EmailParticipant>();

        foreach (var participant in participants)
        {
            var email = TryNormalizeAddress(participant.Email);
            if (email == null || !seen.Add(email))
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(participant.Name) ? null : participant.Name.Trim();
            result.Add(new EmailParticipant(name, email));
        }

        return result;
    }

    public static string? TryNormalizeAddress(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        try
        {
            return new MailAddress(email.Trim()).Address;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// "Reply all" for a ticket, worked out from the latest email on the thread:
    /// <list type="bullet">
    /// <item>Latest email came in (requester side): reply to its sender, copy its To and Cc.</item>
    /// <item>Latest email was a staff reply: reuse that reply's To and Cc.</item>
    /// <item>No email yet (ticket logged by hand): reply to the requester.</item>
    /// </list>
    /// The helpdesk mailbox is never a recipient, and nobody appears twice.
    /// </summary>
    public static ReplyRecipients ResolveReplyAll(
        Ticket ticket,
        IEnumerable<TicketComment> conversation,
        Func<string, bool> isHelpdeskAddress
    )
    {
        var latestEmail = conversation
            .Where(comment =>
                !comment.IsInternalNote
                && (comment.IsFromRequesterSide || comment.ToRecipients.Count > 0)
            )
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id)
            .FirstOrDefault();

        List<EmailParticipant> to;
        List<EmailParticipant> cc;

        if (latestEmail == null)
        {
            to = [];
            cc = [];
        }
        else if (latestEmail.IsFromRequesterSide)
        {
            to = string.IsNullOrWhiteSpace(latestEmail.AuthorEmail)
                ? []
                : [new EmailParticipant(latestEmail.AuthorName, latestEmail.AuthorEmail)];
            cc = [.. latestEmail.ToRecipients, .. latestEmail.CcRecipients];
        }
        else
        {
            to = [.. latestEmail.ToRecipients];
            cc = [.. latestEmail.CcRecipients];
        }

        if (to.Count == 0 && cc.Count == 0 && !string.IsNullOrWhiteSpace(ticket.RequesterEmail))
        {
            to = [new EmailParticipant(ticket.Requester, ticket.RequesterEmail)];
        }

        return Clean(to, cc, isHelpdeskAddress);
    }

    /// <summary>
    /// Normalizes an explicit To/Cc choice: drops the helpdesk mailbox and
    /// duplicates, and promotes the first Cc to To if To ends up empty.
    /// </summary>
    public static ReplyRecipients Clean(
        IEnumerable<EmailParticipant> to,
        IEnumerable<EmailParticipant> cc,
        Func<string, bool> isHelpdeskAddress
    )
    {
        var cleanTo = Normalize(to).Where(p => !isHelpdeskAddress(p.Email)).ToList();
        var toEmails = cleanTo.Select(p => p.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cleanCc = Normalize(cc)
            .Where(p => !isHelpdeskAddress(p.Email) && !toEmails.Contains(p.Email))
            .ToList();

        if (cleanTo.Count == 0 && cleanCc.Count > 0)
        {
            cleanTo.Add(cleanCc[0]);
            cleanCc.RemoveAt(0);
        }

        return new ReplyRecipients(cleanTo, cleanCc);
    }
}
