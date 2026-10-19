using System.Text.RegularExpressions;

namespace Safi_Ticket.Email.Inbound;

/// <summary>
/// Turns an incoming email into the body stored on a ticket. HTML emails keep
/// their HTML so the web app can show them as the sender wrote them (it renders
/// them in a sandboxed frame); plain-text emails lose their quoted history and
/// "Sent from my iPhone" lines.
/// </summary>
public static class EmailBodyCleaner
{
    public static string Clean(string? textBody, string? htmlBody)
    {
        if (!string.IsNullOrWhiteSpace(htmlBody))
        {
            return CleanHtml(htmlBody);
        }

        return CleanPlainText(textBody ?? string.Empty);
    }

    private static string CleanHtml(string body)
    {
        var html = body.Replace("\r\n", "\n").Replace("\r", "\n").Trim();

        const string stylePattern = @"<\s*style\b[^>]*>[\s\S]*?<\s*/\s*style\s*>";
        var styleBlocks = Regex
            .Matches(html, stylePattern, RegexOptions.IgnoreCase)
            .Select(match => match.Value)
            .ToList();

        html = Regex.Replace(html, stylePattern, string.Empty, RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<!--[\s\S]*?-->", string.Empty);
        html = Regex.Replace(
            html,
            @"<\s*(script|head)\b[\s\S]*?<\s*/\s*\1\s*>",
            string.Empty,
            RegexOptions.IgnoreCase
        );
        html = Regex.Replace(
            html,
            @"<\s*(meta|link)\b[^>]*>",
            string.Empty,
            RegexOptions.IgnoreCase
        );

        var bodyMatch = Regex.Match(
            html,
            @"<\s*body\b[^>]*>([\s\S]*?)<\s*/\s*body\s*>",
            RegexOptions.IgnoreCase
        );

        if (bodyMatch.Success)
        {
            html = bodyMatch.Groups[1].Value.Trim();
        }

        // Keep the email's own <style> rules; the web app scopes and filters them.
        return (string.Concat(styleBlocks) + html).Trim();
    }

    private static string CleanPlainText(string body)
    {
        var text = body.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var kept = new List<string>();

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();

            if (StartsQuotedHistory(trimmed))
            {
                break;
            }

            if (trimmed.StartsWith("Sent from my ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            kept.Add(line.TrimEnd());
        }

        var cleaned = string.Join('\n', kept).Trim();
        return cleaned.Length == 0 ? text : cleaned;
    }

    private static bool StartsQuotedHistory(string line) =>
        line.StartsWith('>')
        || (
            line.StartsWith("On ", StringComparison.OrdinalIgnoreCase)
            && line.EndsWith("wrote:", StringComparison.OrdinalIgnoreCase)
        )
        || line.StartsWith("-----Original Message-----", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("From:", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("Sent:", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("To:", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase);
}
