using System.Net;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace Safi_Ticket.Email;

/// <summary>
/// Ticket bodies are either plain text or (for emails with tables) HTML. These
/// helpers put either kind safely into an outgoing email: HTML is sanitized and
/// kept as HTML, plain text is escaped, and both get a plain-text version.
/// </summary>
public static partial class EmailHtml
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    public static bool IsHtml(string? value) =>
        !string.IsNullOrWhiteSpace(value) && HtmlTagPattern().IsMatch(value);

    /// <summary>Body ready to drop into an HTML email: sanitized HTML, or escaped text with line breaks.</summary>
    public static string ToEmailHtml(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        return IsHtml(body) ? Sanitizer.Sanitize(body) : FromPlainText(body);
    }

    /// <summary>Plain text (e.g. a reply typed in the app) as HTML: escaped, with line breaks kept.</summary>
    public static string FromPlainText(string? text) => Encode(text?.Trim()).Replace("\n", "<br>");

    /// <summary>Body as readable plain text, for the text/plain part and previews.</summary>
    public static string ToPlainText(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        if (!IsHtml(body))
        {
            return body.Replace("\r\n", "\n").Trim();
        }

        var text = Regex.Replace(
            body,
            @"<\s*(script|style|head)\b[\s\S]*?<\s*/\s*\1\s*>",
            string.Empty,
            RegexOptions.IgnoreCase
        );
        text = Regex.Replace(text, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(
            text,
            @"<\s*/\s*(p|div|tr|li|h[1-6]|table)\s*>",
            "\n",
            RegexOptions.IgnoreCase
        );
        text = Regex.Replace(text, @"<\s*/\s*(td|th)\s*>", "\t", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+\n", "\n");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        return text.Trim();
    }

    public static string Encode(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty).Replace("\r\n", "\n");

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        // Email tables lean on these legacy presentation attributes.
        foreach (
            var attribute in new[] { "bgcolor", "cellpadding", "cellspacing", "valign", "border" }
        )
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }
        // Links in a forwarded body must not hijack the helpdesk's own pages.
        sanitizer.PostProcessNode += (_, args) =>
        {
            if (args.Node is AngleSharp.Html.Dom.IHtmlAnchorElement anchor)
            {
                anchor.SetAttribute("target", "_blank");
                anchor.SetAttribute("rel", "noopener noreferrer");
            }
        };
        return sanitizer;
    }

    /// <summary>Only real HTML tags count, so "press &lt;Ctrl&gt;" in plain text stays text.</summary>
    [GeneratedRegex(
        @"<\/?(p|div|br|span|table|thead|tbody|tr|td|th|a|b|i|u|strong|em|ul|ol|li|h[1-6]|font|img|blockquote|hr|html|body|center|pre|style)\b[^>]*>",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex HtmlTagPattern();
}
