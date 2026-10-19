using Microsoft.AspNetCore.Mvc;
using Safi_Ticket.Common.Auth;
using Safi_Ticket.Common.Storage;

namespace Safi_Ticket.Features.Tickets;

[ApiController]
[Route("api/[controller]")]
[AllowRoles(Roles.Admin, Roles.Officer)]
public class TicketController(
    TicketQueries queries,
    TicketService tickets,
    AttachmentStorage storage
) : ControllerBase
{
    /// <summary>The ticket list. Officers only ever see tickets assigned to them.</summary>
    [HttpGet]
    public async Task<TicketSearchResponse> GetTickets([FromQuery] TicketQueryRequest request)
    {
        if (!User.IsAdmin())
        {
            request.UserId = User.GetUserId();
        }

        return await queries.SearchAsync(request, User.GetUserId());
    }

    [HttpGet("{ticketId:int}")]
    public async Task<TicketDetailResponse> GetTicket(int ticketId) =>
        await queries.GetDetailAsync(ticketId, User.GetUserId());

    [HttpPost]
    public async Task<string> CreateTicket(CreateTicketRequest request)
    {
        await tickets.CreateAsync(request, User.ToActor());
        return "Ticket created";
    }

    [HttpPost("with-attachments")]
    [Consumes("multipart/form-data")]
    public async Task<CreatedTicketResponse> CreateTicketWithAttachments(
        [FromForm] CreateTicketWithAttachmentsRequest request
    )
    {
        var ticket = await tickets.CreateAsync(request, User.ToActor(), request.Attachments);
        var attachments = await queries.GetAttachmentsAsync(ticket.Id);

        return new CreatedTicketResponse(
            ticket.Id,
            ticket.Title,
            ticket.Body,
            ticket.Requester,
            ticket.RequesterEmail,
            ticket.StatusId,
            ticket.UserId,
            ticket.CreatedAt,
            attachments
        );
    }

    [HttpPut("{ticketId:int}")]
    public async Task<TicketListItemResponse> UpdateTicket(
        int ticketId,
        UpdateTicketRequest request
    )
    {
        await tickets.UpdateAsync(ticketId, request, User.ToActor());
        return await queries.GetListItemAsync(ticketId, User.GetUserId());
    }

    [HttpGet("{ticketId:int}/comments")]
    public async Task<List<TicketCommentResponse>> GetComments(int ticketId) =>
        await queries.GetCommentsAsync(ticketId);

    [HttpPost("{ticketId:int}/comments")]
    public async Task<TicketCommentResponse> AddComment(
        int ticketId,
        CreateCommentRequest request
    ) => await tickets.AddCommentAsync(ticketId, request, User.ToActor());

    [HttpPost("{ticketId:int}/reply")]
    public async Task<TicketCommentResponse> Reply(int ticketId, CreateReplyRequest request) =>
        await tickets.ReplyAsync(ticketId, request, User.ToActor());

    [HttpPost("{ticketId:int}/close")]
    public async Task<TicketListItemResponse> CloseTicket(int ticketId, CloseTicketRequest request)
    {
        await tickets.CloseAsync(ticketId, request, User.ToActor());
        return await queries.GetListItemAsync(ticketId, User.GetUserId());
    }

    /// <summary>Cancels (soft-closes) a ticket. Admins only.</summary>
    [HttpDelete("{ticketId:int}")]
    [AllowRoles(Roles.Admin)]
    public async Task<TicketListItemResponse> CancelTicket(int ticketId)
    {
        await tickets.CancelAsync(ticketId);
        return await queries.GetListItemAsync(ticketId, User.GetUserId());
    }

    [HttpPost("{ticketId:int}/read")]
    public async Task<IActionResult> MarkRequesterRepliesAsRead(int ticketId)
    {
        await tickets.MarkRequesterRepliesAsReadAsync(ticketId, User.GetUserId());
        return NoContent();
    }

    [HttpGet("{ticketId:int}/attachments")]
    public async Task<List<TicketAttachmentResponse>> GetAttachments(int ticketId) =>
        await queries.GetAttachmentsAsync(ticketId);

    [HttpPost("{ticketId:int}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<TicketAttachmentResponse> AddAttachment(int ticketId, IFormFile file) =>
        TicketAttachmentResponse.From(await tickets.AddAttachmentAsync(ticketId, file));

    [HttpGet("attachments/{attachmentId:int}/file")]
    public async Task<IActionResult> DownloadAttachment(int attachmentId)
    {
        var attachment = await queries.GetAttachmentAsync(attachmentId);
        var filePath = storage.ResolvePath(attachment);

        if (filePath == null)
        {
            return NotFound(
                new { message = $"The file for {attachment.FileName} is missing on the server." }
            );
        }

        return PhysicalFile(
            filePath,
            string.IsNullOrWhiteSpace(attachment.ContentType)
                ? "application/octet-stream"
                : attachment.ContentType,
            attachment.FileName,
            enableRangeProcessing: true
        );
    }
}
