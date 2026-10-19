using Safi_Ticket.Models;

namespace Safi_Ticket.Email;

/// <summary>What one email says. Text fields are plain text; <see cref="MessageHtml"/> is ready-made HTML.</summary>
public sealed record EmailContent
{
    /// <summary>Preview line shown next to the subject in most inboxes.</summary>
    public required string Preheader { get; init; }

    public Ticket? Ticket { get; init; }
    public string? StatusName { get; init; }

    /// <summary>Large heading, used when there is no ticket stub (e.g. password reset).</summary>
    public string? Heading { get; init; }

    public string? Greeting { get; init; }
    public string? Intro { get; init; }

    /// <summary>Author shown above a personal message (staff replies, closing notes).</summary>
    public string? MessageAuthor { get; init; }
    public string? MessageLabel { get; init; }
    public string? MessageHtml { get; init; }

    /// <summary>A highlighted note asking the reader to do something (e.g. email the helpdesk).</summary>
    public EmailCallout? Callout { get; init; }

    public IReadOnlyList<(string Label, string Value)> Details { get; init; } = [];
    public string? Code { get; init; }
    public string? ActionLabel { get; init; }
    public string? ActionUrl { get; init; }
    public required string Footnote { get; init; }
}

/// <summary>"{Lead} {Email}" in bold, with the address as a mailto link, then a line of explanation.</summary>
public sealed record EmailCallout(string Lead, string Email, string Text);

/// <summary>
/// Renders the branded HTML email. Mirrors the web app's design tokens
/// (Client/src/styles.css) - paper background, ink text, the ticket-stub
/// header - so an email reads as the same product as the dashboard. Built
/// with tables and inline styles because that is what email clients support.
/// </summary>
public static class EmailTemplates
{
    public const string LogoContentId = "safi-ticket-logo@safi-ticket.local";

    private const string Paper = "#eef0f3";
    private const string PaperSoft = "#f6f7f9";
    private const string Sheet = "#ffffff";
    private const string Ink = "#18203a";
    private const string InkSoft = "#2e3754";
    private const string InkMuted = "#586079";
    private const string InkSubtle = "#868ca0";
    private const string Line = "#dce0e7";
    private const string LineSoft = "#e9ecf0";
    private const string LineStrong = "#c9ced8";
    private const string Cobalt = "#2d4bd8";
    private const string MarkerSoft = "#fff8d1";
    private const string MarkerLine = "#ffe892";
    private const string Font =
        "'Segoe UI',-apple-system,BlinkMacSystemFont,Helvetica,Arial,sans-serif";
    private const string DisplayFont = "Jost,'Segoe UI',-apple-system,Helvetica,Arial,sans-serif";

    public static string Render(EmailContent content)
    {
        var body = string.Concat(
            HeadingHtml(content),
            ParagraphHtml(content.Greeting, Ink, "0 0 12px 0"),
            ParagraphHtml(content.Intro, InkMuted, "0"),
            CalloutHtml(content.Callout),
            MessageHtml(content),
            DetailsHtml(content.Details),
            CodeHtml(content.Code),
            ActionHtml(content.ActionLabel, content.ActionUrl)
        );

        return $$"""
            <!doctype html>
            <html lang="en">
              <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta name="color-scheme" content="light">
                <title>{{EmailHtml.Encode(content.Preheader)}}</title>
              </head>
              <body style="margin:0;padding:0;background:{{Paper}};-webkit-font-smoothing:antialiased;">
                <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:{{Paper}};">{{EmailHtml.Encode(
                content.Preheader
            )}}</div>
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:{{Paper}};">
                  <tr>
                    <td align="center" style="padding:32px 12px 40px 12px;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;">
                        <tr>
                          <td style="padding:0 4px 18px 4px;">
                            <img src="cid:{{LogoContentId}}" alt="Safi Ticket, IT Department" width="132" style="display:block;width:132px;max-width:100%;height:auto;border:0;">
                          </td>
                        </tr>
                        <tr>
                          <td style="background:{{Sheet}};border:1px solid {{Line}};border-radius:14px;">
                            {{StubHtml(content)}}
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                              <tr>
                                <td style="padding:28px 30px 30px 30px;font-family:{{Font}};">
                                  {{body}}
                                </td>
                              </tr>
                              <tr>
                                <td style="padding:16px 30px 20px 30px;border-top:1px solid {{LineSoft}};font-family:{{Font}};font-size:12.5px;line-height:1.6;color:{{InkSubtle}};">
                                  {{EmailHtml.Encode(content.Footnote)}}
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td align="center" style="padding:18px 4px 0 4px;font-family:{{Font}};font-size:12px;line-height:1.6;color:{{InkSubtle}};">
                            Safi IT Department
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>
              </body>
            </html>
            """;
    }

    /// <summary>The ticket stub: number on the left, dashed tear line, then title and status.</summary>
    private static string StubHtml(EmailContent content)
    {
        if (content.Ticket is not { } ticket)
        {
            return string.Empty;
        }

        var status = string.IsNullOrWhiteSpace(content.StatusName)
            ? string.Empty
            : $"""&nbsp;&nbsp;{StatusChipHtml(content.StatusName)}""";

        return $$"""
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-bottom:1px solid {{LineSoft}};">
              <tr>
                <td width="96" valign="middle" style="width:96px;padding:22px 0 22px 26px;font-family:{{Font}};">
                  <div style="font-size:12px;line-height:1.4;color:{{InkMuted}};">Ticket</div>
                  <div style="font-family:{{DisplayFont}};font-size:34px;line-height:1.1;font-weight:300;letter-spacing:-0.02em;color:{{Ink}};">{{ticket.Id}}</div>
                </td>
                <td valign="middle" style="padding:22px 26px 22px 22px;border-left:1px dashed {{LineStrong}};font-family:{{Font}};">
                  <div style="font-family:{{DisplayFont}};font-size:19px;line-height:1.35;font-weight:400;color:{{Ink}};">{{EmailHtml.Encode(
                ticket.Title
            )}}</div>
                  <div style="margin-top:6px;font-size:13px;line-height:1.5;color:{{InkMuted}};">Requested by {{EmailHtml.Encode(
                ticket.Requester
            )}}{{status}}</div>
                </td>
              </tr>
            </table>
            """;
    }

    private static string StatusChipHtml(string statusName)
    {
        var (background, color) = statusName switch
        {
            TicketStatuses.Closed => ("#e7f3ee", "#1f7a5a"),
            TicketStatuses.Cancelled => ("#fbedef", "#b23b4e"),
            TicketStatuses.InProgress => ("#edf0fd", "#2d4bd8"),
            _ => ("#eef0f5", "#5e6a85"),
        };

        return $"""<span style="display:inline-block;padding:2px 9px;border-radius:999px;background:{background};color:{color};font-size:12px;font-weight:600;line-height:1.6;">{EmailHtml.Encode(statusName)}</span>""";
    }

    private static string HeadingHtml(EmailContent content) =>
        string.IsNullOrWhiteSpace(content.Heading)
            ? string.Empty
            : $"""<h1 style="margin:0 0 18px 0;font-family:{DisplayFont};font-size:26px;line-height:1.25;font-weight:300;letter-spacing:-0.01em;color:{Ink};">{EmailHtml.Encode(content.Heading)}</h1>""";

    private static string ParagraphHtml(string? text, string color, string margin) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : $"""<p style="margin:{margin};font-size:15px;line-height:1.65;color:{color};">{EmailHtml.Encode(text).Replace("\n", "<br>")}</p>""";

    /// <summary>
    /// A personal message (staff reply) reads like a letter: author initials
    /// and name, then the words. Other content sits in a soft panel.
    /// </summary>
    private static string MessageHtml(EmailContent content)
    {
        if (string.IsNullOrWhiteSpace(content.MessageHtml))
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(content.MessageAuthor))
        {
            var hasLeadIn =
                !string.IsNullOrWhiteSpace(content.Greeting)
                || !string.IsNullOrWhiteSpace(content.Intro);

            return $$"""
                <table role="presentation" cellspacing="0" cellpadding="0" style="margin:{{(
                    hasLeadIn ? "22px" : "0"
                )}} 0 14px 0;">
                  <tr>
                    <td width="34" height="34" align="center" valign="middle" style="width:34px;height:34px;border-radius:17px;background:{{Ink}};color:#ffffff;font-family:{{DisplayFont}};font-size:12px;font-weight:500;letter-spacing:0.02em;">{{EmailHtml.Encode(
                    Initials(content.MessageAuthor)
                )}}</td>
                    <td style="padding-left:10px;font-family:{{Font}};">
                      <div style="font-size:14px;line-height:1.3;font-weight:600;color:{{Ink}};">{{EmailHtml.Encode(
                    content.MessageAuthor
                )}}</div>
                      <div style="font-size:12px;line-height:1.4;color:{{InkSubtle}};">Safi IT Department</div>
                    </td>
                  </tr>
                </table>
                <div style="font-family:{{Font}};font-size:15px;line-height:1.7;color:{{InkSoft}};">{{content.MessageHtml}}</div>
                """;
        }

        var label = string.IsNullOrWhiteSpace(content.MessageLabel)
            ? string.Empty
            : $"""<div style="margin:0 0 8px 0;font-size:13px;font-weight:600;color:{Ink};">{EmailHtml.Encode(content.MessageLabel)}</div>""";

        return $$"""
            <div style="margin:24px 0 0 0;">
              {{label}}
              <div style="padding:16px 18px;background:{{PaperSoft}};border:1px solid {{LineSoft}};border-radius:12px;font-family:{{Font}};font-size:14px;line-height:1.65;color:{{InkSoft}};overflow-x:auto;">{{content.MessageHtml}}</div>
            </div>
            """;
    }

    /// <summary>Highlighter-yellow note, the same colour the app uses for "look here".</summary>
    private static string CalloutHtml(EmailCallout? callout)
    {
        if (callout == null || string.IsNullOrWhiteSpace(callout.Email))
        {
            return string.Empty;
        }

        var email = EmailHtml.Encode(callout.Email);

        return $$"""
            <div style="margin:22px 0 0 0;padding:16px 18px;background:{{MarkerSoft}};border:1px solid {{MarkerLine}};border-radius:12px;font-family:{{Font}};">
              <div style="font-size:15px;line-height:1.5;font-weight:600;color:{{Ink}};">{{EmailHtml.Encode(
                callout.Lead
            )}} <a href="mailto:{{email}}" style="color:{{Cobalt}};text-decoration:underline;">{{email}}</a></div>
              <div style="margin-top:6px;font-size:14px;line-height:1.6;color:{{InkSoft}};">{{EmailHtml.Encode(
                callout.Text
            )}}</div>
            </div>
            """;
    }

    private static string DetailsHtml(IReadOnlyList<(string Label, string Value)> details)
    {
        if (details.Count == 0)
        {
            return string.Empty;
        }

        var rows = string.Concat(
            details.Select(detail =>
                $$"""
                    <tr>
                      <td width="120" style="width:120px;padding:9px 12px 9px 0;border-top:1px solid {{LineSoft}};font-size:13px;line-height:1.5;color:{{InkMuted}};">{{EmailHtml.Encode(
                        detail.Label
                    )}}</td>
                      <td style="padding:9px 0;border-top:1px solid {{LineSoft}};font-size:13px;line-height:1.5;font-weight:600;color:{{Ink}};">{{EmailHtml.Encode(
                        detail.Value
                    )}}</td>
                    </tr>
                    """
            )
        );

        return $"""<table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:22px 0 0 0;font-family:{Font};">{rows}</table>""";
    }

    private static string CodeHtml(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? string.Empty
            : $"""<div style="margin:22px 0 0 0;padding:18px;background:{PaperSoft};border:1px solid {LineSoft};border-radius:12px;text-align:center;font-family:{DisplayFont};font-size:32px;line-height:1.2;font-weight:400;letter-spacing:0.3em;color:{Ink};">{EmailHtml.Encode(code)}</div>""";

    /// <summary>An ink button (table-based so Outlook renders it).</summary>
    private static string ActionHtml(string? label, string? url)
    {
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        return $$"""
            <table role="presentation" cellspacing="0" cellpadding="0" style="margin:26px 0 0 0;">
              <tr>
                <td style="border-radius:8px;background:{{Ink}};">
                  <a href="{{EmailHtml.Encode(
                url
            )}}" target="_blank" style="display:inline-block;padding:11px 20px;font-family:{{Font}};font-size:14px;font-weight:600;line-height:1.2;color:#ffffff;text-decoration:none;border-radius:8px;">{{EmailHtml.Encode(
                label
            )}}</a>
                </td>
              </tr>
            </table>
            """;
    }

    private static string Initials(string name)
    {
        var parts = name.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant(),
        };
    }
}
