using Safi_Ticket.Models;

namespace Safi_Ticket.Common.Storage;

/// <summary>
/// Stores ticket attachments on disk under <c>wwwroot/uploads/tickets/{ticketId}</c>
/// and builds the matching <see cref="TicketAttachment"/> rows. Callers add the
/// rows to the context; this class only touches the file system.
/// </summary>
public sealed class AttachmentStorage(IWebHostEnvironment environment)
{
    private string WebRootPath =>
        string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;

    /// <summary>Writes one file and returns its (unsaved) attachment row.</summary>
    public async Task<TicketAttachment> SaveAsync(
        int ticketId,
        string fileName,
        string? contentType,
        Func<Stream, Task> writeContent
    )
    {
        var ticketFolder = Path.Combine(WebRootPath, "uploads", "tickets", ticketId.ToString());
        Directory.CreateDirectory(ticketFolder);

        var originalFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            originalFileName = "attachment";
        }

        var storedFileName = $"{Guid.NewGuid():N}{Path.GetExtension(originalFileName)}";
        var filePath = Path.Combine(ticketFolder, storedFileName);

        await using (var stream = File.Create(filePath))
        {
            await writeContent(stream);
        }

        return new TicketAttachment
        {
            TicketId = ticketId,
            FileName = originalFileName,
            StoredFileName = storedFileName,
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType,
            SizeBytes = new FileInfo(filePath).Length,
            FilePath = filePath,
            Url = $"/uploads/tickets/{ticketId}/{storedFileName}",
            UploadedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Finds the attachment's file on disk. Older rows may carry an absolute path
    /// from another machine, so the URL and stored name are tried as well.
    /// </summary>
    public string? ResolvePath(TicketAttachment attachment)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(attachment.FilePath))
        {
            candidates.Add(attachment.FilePath);
        }

        if (!string.IsNullOrWhiteSpace(attachment.Url))
        {
            candidates.Add(
                Path.Combine(
                    WebRootPath,
                    attachment.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(attachment.StoredFileName))
        {
            candidates.Add(
                Path.Combine(
                    WebRootPath,
                    "uploads",
                    "tickets",
                    attachment.TicketId.ToString(),
                    attachment.StoredFileName
                )
            );
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>The web root itself, for other static assets (e.g. the email logo).</summary>
    public string GetWebRootFile(params string[] segments) =>
        Path.GetFullPath(Path.Combine([WebRootPath, .. segments]));
}
