using Microsoft.AspNetCore.Mvc;
using Safi_Ticket.Authorization;
using Safi_Ticket.DTO.Request;
using Safi_Ticket.Models;
using Safi_Ticket.Services;

namespace Safi_Ticket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowRoles("Admin", "Officer")]
    public class TicketController : ControllerBase
    {
        private readonly TicketService _ticketService;

        public TicketController(TicketService ticketService)
        {
            _ticketService = ticketService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTickets([FromQuery] TicketQueryRequest request)
        {
            var tickets = await _ticketService.SearchTicketsAsync(request);

            return Ok(tickets);
        }

        [HttpGet("{ticketId:int}")]
        public async Task<IActionResult> GetTicket(int ticketId)
        {
            var ticket = await _ticketService.GetTicketByIdAsync(ticketId);

            if (ticket == null)
            {
                return NotFound($"Ticket with id {ticketId} was not found.");
            }

            return Ok(ticket);
        }

        [HttpPost]
        public async Task<ActionResult<string>> CreateTicket(CreateTicketRequest request)
        {
            try
            {
                await _ticketService.CreateTicketAsync(request);

                return Ok("Ticket created");
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpPost("with-attachments")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateTicketWithAttachments(
            [FromForm] CreateTicketWithAttachmentsRequest request
        )
        {
            try
            {
                var ticket = await _ticketService.CreateTicketWithAttachmentsAsync(request);
                var attachments = await _ticketService.GetAttachmentsByTicketIdAsync(ticket.Id);

                return Ok(
                    new
                    {
                        ticket.Id,
                        ticket.Title,
                        ticket.Body,
                        ticket.Requester,
                        ticket.RequesterEmail,
                        ticket.StatusId,
                        ticket.UserId,
                        ticket.CreatedAt,
                        Attachments = attachments.Select(ToAttachmentResponse),
                    }
                );
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateTicket(int id, UpdateTicketRequest request)
        {
            try
            {
                var result = await _ticketService.UpdateTicketAsync(id, request);

                if (result == null)
                {
                    return NotFound($"Ticket with id {id} was not found.");
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden." });
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpPost("{ticketId:int}/comments")]
        public async Task<IActionResult> AddComment(
            int ticketId,
            CreateTicketCommentRequest request
        )
        {
            var comment = await _ticketService.AddCommentAsync(ticketId, request);

            if (comment == null)
            {
                return NotFound($"Ticket with id {ticketId} was not found.");
            }

            return Ok(comment);
        }

        [HttpGet("{ticketId:int}/comments")]
        public async Task<IActionResult> GetComments(int ticketId)
        {
            var comments = await _ticketService.GetCommentsByTicketIdAsync(ticketId);

            return Ok(comments);
        }

        [HttpGet("{ticketId:int}/attachments")]
        public async Task<IActionResult> GetAttachments(int ticketId)
        {
            var attachments = await _ticketService.GetAttachmentsByTicketIdAsync(ticketId);

            return Ok(attachments.Select(ToAttachmentResponse));
        }

        [HttpGet("attachments/{attachmentId:int}/file")]
        public async Task<IActionResult> DownloadAttachment(int attachmentId)
        {
            var attachment = await _ticketService.GetAttachmentByIdAsync(attachmentId);

            if (attachment == null)
            {
                return NotFound($"Attachment with id {attachmentId} was not found.");
            }

            var filePath = _ticketService.ResolveAttachmentFilePath(attachment);

            if (filePath == null)
            {
                return NotFound($"Attachment file for id {attachmentId} was not found.");
            }

            var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                ? "application/octet-stream"
                : attachment.ContentType;

            return PhysicalFile(
                filePath,
                contentType,
                attachment.FileName,
                enableRangeProcessing: true
            );
        }

        [HttpPost("{ticketId:int}/attachments")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AddAttachment(int ticketId, IFormFile file)
        {
            var attachment = await _ticketService.AddAttachmentAsync(ticketId, file);

            if (attachment == null)
            {
                return NotFound($"Ticket with id {ticketId} was not found.");
            }

            return Ok(ToAttachmentResponse(attachment));
        }

        [HttpPost("{ticketId:int}/reply")]
        public async Task<IActionResult> ReplyToRequester(
            int ticketId,
            CreateTicketReplyRequest request
        )
        {
            try
            {
                var reply = await _ticketService.ReplyToRequesterAsync(ticketId, request);

                if (reply == null)
                {
                    return NotFound($"Ticket with id {ticketId} was not found.");
                }

                return Ok(
                    new
                    {
                        reply.Id,
                        reply.TicketId,
                        reply.Body,
                        reply.AuthorName,
                        reply.AuthorEmail,
                        reply.AuthorType,
                        reply.IsInternalNote,
                        reply.UserId,
                        reply.CreatedAt,
                    }
                );
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden." });
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpPost("{ticketId:int}/close")]
        public async Task<IActionResult> CloseTicket(int ticketId, CloseTicketRequest request)
        {
            try
            {
                var ticket = await _ticketService.CloseTicketAsync(ticketId, request);

                if (ticket == null)
                {
                    return NotFound($"Ticket with id {ticketId} was not found.");
                }

                return Ok(ticket);
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpDelete("{ticketId:int}")]
        [AllowRoles("Admin")]
        public async Task<IActionResult> DeleteTicket(int ticketId)
        {
            try
            {
                var result = await _ticketService.CancelTicketAsync(ticketId);

                if (result == null)
                {
                    return NotFound($"Ticket with id {ticketId} was not found.");
                }

                return Ok(result);
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        private static object ToAttachmentResponse(TicketAttachment attachment)
        {
            return new
            {
                attachment.Id,
                attachment.TicketId,
                attachment.FileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.Url,
                DownloadUrl = $"/api/ticket/attachments/{attachment.Id}/file",
                attachment.UploadedAt,
            };
        }
    }
}
