using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using Safi_Ticket.Data;
using Safi_Ticket.DTO.Request;
using Safi_Ticket.DTO.Response;
using Safi_Ticket.Models;

namespace Safi_Ticket.Services
{
    public class TicketService
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailNotificationService _emailNotificationService;
        private readonly BackgroundEmailQueue _backgroundEmailQueue;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<TicketService> _logger;

        public TicketService(
            ApplicationDbContext context,
            EmailNotificationService emailNotificationService,
            BackgroundEmailQueue backgroundEmailQueue,
            IWebHostEnvironment environment,
            ILogger<TicketService> logger
        )
        {
            _context = context;
            _emailNotificationService = emailNotificationService;
            _backgroundEmailQueue = backgroundEmailQueue;
            _environment = environment;
            _logger = logger;
        }

        public async Task<string> CreateTicketAsync(CreateTicketRequest request)
        {
            await CreateTicketEntityAsync(request);

            return "Ticket has been submitted successfully.";
        }

        public async Task<Ticket> CreateTicketWithAttachmentsAsync(
            CreateTicketWithAttachmentsRequest request
        )
        {
            var ticket = await CreateTicketEntityAsync(request);
            await SaveAttachmentsAsync(ticket.Id, request.Attachments);

            return ticket;
        }

        private async Task<Ticket> CreateTicketEntityAsync(CreateTicketRequest request)
        {
            var title = request.Title.Trim();
            var body = request.Body.Trim();
            var requester = request.Requester.Trim();
            var requesterEmail = NormalizeRequesterEmail(request.RequesterEmail);

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new InvalidOperationException("Ticket title is required.");
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                throw new InvalidOperationException("Ticket body is required.");
            }

            if (string.IsNullOrWhiteSpace(requester))
            {
                throw new InvalidOperationException("Requester is required.");
            }

            var ticket = new Ticket
            {
                Title = title,
                Body = body,
                Requester = requester,
                RequesterEmail = requesterEmail,
                StatusId = await GetStatusIdByNameAsync("Initiated"),
                UserId = null,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
            };

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            return ticket;
        }

        private static string NormalizeRequesterEmail(string? email)
        {
            var requesterEmail = email?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(requesterEmail))
            {
                throw new InvalidOperationException("Requester email is required.");
            }

            try
            {
                return new MailAddress(requesterEmail).Address;
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Requester email is invalid.");
            }
        }

        private async Task<int> GetStatusIdByNameAsync(string statusName)
        {
            var statusId = await _context
                .Statuses.AsNoTracking()
                .Where(status => status.Name == statusName)
                .Select(status => (int?)status.Id)
                .FirstOrDefaultAsync();

            if (!statusId.HasValue)
            {
                throw new InvalidOperationException($"The {statusName} status does not exist.");
            }

            return statusId.Value;
        }

        public async Task<List<TicketAttachment>> GetAttachmentsByTicketIdAsync(int ticketId)
        {
            return await _context
                .TicketAttachments.AsNoTracking()
                .Where(attachment => attachment.TicketId == ticketId)
                .OrderBy(attachment => attachment.UploadedAt)
                .ToListAsync();
        }

        public async Task<TicketAttachment?> GetAttachmentByIdAsync(int attachmentId)
        {
            return await _context
                .TicketAttachments.AsNoTracking()
                .FirstOrDefaultAsync(attachment => attachment.Id == attachmentId);
        }

        public string? ResolveAttachmentFilePath(TicketAttachment attachment)
        {
            var candidatePaths = new List<string>();

            if (!string.IsNullOrWhiteSpace(attachment.FilePath))
            {
                candidatePaths.Add(attachment.FilePath);
            }

            var webRootPath = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            if (!string.IsNullOrWhiteSpace(attachment.Url))
            {
                candidatePaths.Add(
                    Path.Combine(
                        webRootPath,
                        attachment.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                    )
                );
            }

            if (!string.IsNullOrWhiteSpace(attachment.StoredFileName))
            {
                candidatePaths.Add(
                    Path.Combine(
                        webRootPath,
                        "uploads",
                        "tickets",
                        attachment.TicketId.ToString(),
                        attachment.StoredFileName
                    )
                );
            }

            return candidatePaths.FirstOrDefault(File.Exists);
        }

        public async Task<TicketAttachment?> AddAttachmentAsync(int ticketId, IFormFile file)
        {
            var ticketExists = await _context.Tickets.AnyAsync(ticket =>
                ticket.Id == ticketId && !ticket.IsDeleted
            );

            if (!ticketExists)
            {
                return null;
            }

            var attachments = await SaveAttachmentsAsync(ticketId, new[] { file });
            return attachments.FirstOrDefault();
        }

        private async Task<List<TicketAttachment>> SaveAttachmentsAsync(
            int ticketId,
            IEnumerable<IFormFile> files
        )
        {
            var validFiles = files.Where(file => file.Length > 0).ToList();

            if (validFiles.Count == 0)
            {
                return new List<TicketAttachment>();
            }

            var webRootPath = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            var ticketFolder = Path.Combine(webRootPath, "uploads", "tickets", ticketId.ToString());
            Directory.CreateDirectory(ticketFolder);

            var attachments = new List<TicketAttachment>();

            foreach (var file in validFiles)
            {
                var originalFileName = Path.GetFileName(file.FileName);
                var extension = Path.GetExtension(originalFileName);
                var storedFileName = $"{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(ticketFolder, storedFileName);

                await using (var stream = File.Create(filePath))
                {
                    await file.CopyToAsync(stream);
                }

                attachments.Add(
                    new TicketAttachment
                    {
                        TicketId = ticketId,
                        FileName = originalFileName,
                        StoredFileName = storedFileName,
                        ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                            ? "application/octet-stream"
                            : file.ContentType,
                        SizeBytes = file.Length,
                        FilePath = filePath,
                        Url = $"/uploads/tickets/{ticketId}/{storedFileName}",
                        UploadedAt = DateTime.UtcNow,
                    }
                );
            }

            _context.TicketAttachments.AddRange(attachments);
            await _context.SaveChangesAsync();

            return attachments;
        }

        public async Task<TicketListItemResponse?> GetTicketByIdAsync(int ticketId)
        {
            return await _context
                .Tickets.AsNoTracking()
                .Where(ticket => ticket.Id == ticketId && !ticket.IsDeleted)
                .Select(ticket => new TicketListItemResponse
                {
                    Id = ticket.Id,
                    Title = ticket.Title,
                    Body = ticket.Body,
                    Requester = ticket.Requester,
                    RequesterEmail = ticket.RequesterEmail,
                    StatusId = ticket.StatusId,
                    Status = ticket.Status != null ? ticket.Status.Name : null,
                    UserId = ticket.UserId,
                    Assignee = ticket.User != null ? ticket.User.Name : null,
                    CreatedAt = ticket.CreatedAt,
                    CommentCount = ticket.Comments.Count,
                    LastCommentAt = ticket
                        .Comments.OrderByDescending(comment => comment.CreatedAt)
                        .Select(comment => (DateTime?)comment.CreatedAt)
                        .FirstOrDefault(),
                })
                .FirstOrDefaultAsync();
        }

        public async Task<TicketSearchResponse> SearchTicketsAsync(TicketQueryRequest request)
        {
            var page = Math.Max(1, request.Page);
            const int pageSize = 50;
            var search = request.Search?.Trim();
            var normalizedStartDate = request.StartDate.HasValue
                ? DateTime.SpecifyKind(request.StartDate.Value.Date, DateTimeKind.Utc)
                : (DateTime?)null;
            var normalizedEndDate = request.EndDate.HasValue
                ? DateTime.SpecifyKind(request.EndDate.Value.Date, DateTimeKind.Utc)
                : (DateTime?)null;
            var exclusiveEndDate = normalizedEndDate?.AddDays(1);
            var statusIds = request.StatusIds.Where(statusId => statusId > 0).Distinct().ToList();

            if (request.StatusId.HasValue && request.StatusId.Value > 0)
            {
                statusIds.Add(request.StatusId.Value);
                statusIds = statusIds.Distinct().ToList();
            }

            var baseQuery = _context.Tickets.AsNoTracking().Where(ticket => !ticket.IsDeleted);

            var filteredQuery = baseQuery;

            if (statusIds.Count > 0)
            {
                filteredQuery = filteredQuery.Where(ticket => statusIds.Contains(ticket.StatusId));
            }

            if (request.UserId.HasValue)
            {
                filteredQuery = filteredQuery.Where(ticket => ticket.UserId == request.UserId.Value);
            }

            if (normalizedStartDate.HasValue)
            {
                filteredQuery = filteredQuery.Where(ticket =>
                    ticket.CreatedAt >= normalizedStartDate.Value
                );
            }

            if (exclusiveEndDate.HasValue)
            {
                filteredQuery = filteredQuery.Where(ticket =>
                    ticket.CreatedAt < exclusiveEndDate.Value
                );
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search}%";
                var hasTicketId = int.TryParse(
                    search.Replace("TK-", string.Empty, StringComparison.OrdinalIgnoreCase),
                    out var ticketId
                );

                filteredQuery = filteredQuery.Where(ticket =>
                    EF.Functions.ILike(ticket.Title, pattern)
                    || EF.Functions.ILike(ticket.Body, pattern)
                    || EF.Functions.ILike(ticket.Requester, pattern)
                    || (
                        ticket.RequesterEmail != null
                        && EF.Functions.ILike(ticket.RequesterEmail, pattern)
                    )
                    || (hasTicketId && ticket.Id == ticketId)
                );
            }

            var totalCount = await filteredQuery.CountAsync();

            var items = await filteredQuery
                .OrderByDescending(ticket => ticket.CreatedAt)
                .ThenByDescending(ticket => ticket.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ticket => new TicketListItemResponse
                {
                    Id = ticket.Id,
                    Title = ticket.Title,
                    Body = ticket.Body,
                    Requester = ticket.Requester,
                    RequesterEmail = ticket.RequesterEmail,
                    StatusId = ticket.StatusId,
                    Status = ticket.Status != null ? ticket.Status.Name : null,
                    UserId = ticket.UserId,
                    Assignee = ticket.User != null ? ticket.User.Name : null,
                    CreatedAt = ticket.CreatedAt,
                    CommentCount = ticket.Comments.Count,
                    LastCommentAt = ticket
                        .Comments.OrderByDescending(comment => comment.CreatedAt)
                        .Select(comment => (DateTime?)comment.CreatedAt)
                        .FirstOrDefault(),
                })
                .ToListAsync();

            return new TicketSearchResponse
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }

        public async Task<TicketListItemResponse?> UpdateTicketAsync(int id, UpdateTicketRequest request)
        {
            var ticket = await _context.Tickets
                .FirstOrDefaultAsync(ticket => ticket.Id == id && !ticket.IsDeleted);

            if (ticket == null)
            {
                return null;
            }

            var currentStatus = await _context.Statuses
                .AsNoTracking()
                .FirstOrDefaultAsync(status => status.Id == ticket.StatusId);

            if (currentStatus?.Name is "Closed" or "Cancelled")
            {
                throw new InvalidOperationException(
                    "Closed or cancelled tickets cannot be updated."
                );
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                ticket.Title = request.Title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Body))
            {
                ticket.Body = request.Body.Trim();
            }

            User? assignee = null;

            if (request.UserId.HasValue)
            {
                assignee = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(user => user.Id == request.UserId.Value);

                if (assignee == null)
                {
                    throw new InvalidOperationException("The selected assignee does not exist.");
                }

                ticket.UserId = request.UserId.Value;
                ticket.StatusId = await GetStatusIdByNameAsync("In Progress");
            }

            await _context.SaveChangesAsync();

            if (assignee != null)
            {
                try
                {
                    await _emailNotificationService.SendTicketAssignedAsync(ticket, assignee);
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Failed to send assignment notification for ticket {TicketId}.",
                        ticket.Id
                    );
                }
            }

            return await GetTicketByIdAsync(ticket.Id);
        }

        public async Task<TicketListItemResponse?> CloseTicketAsync(
            int ticketId,
            CloseTicketRequest request
        )
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(ticket =>
                ticket.Id == ticketId && !ticket.IsDeleted
            );

            if (ticket == null)
            {
                return null;
            }

            var body = request.Body.Trim();

            var currentStatus = await _context
                .Statuses.AsNoTracking()
                .FirstOrDefaultAsync(status => status.Id == ticket.StatusId);

            if (currentStatus?.Name == "Cancelled")
            {
                throw new InvalidOperationException("Cancelled tickets cannot be closed.");
            }

            if (currentStatus?.Name == "Closed")
            {
                throw new InvalidOperationException("Ticket is already closed.");
            }

            ticket.StatusId = await GetStatusIdByNameAsync("Closed");

            if (!string.IsNullOrWhiteSpace(body))
            {
                _context.TicketComments.Add(
                    new TicketComment
                    {
                        TicketId = ticket.Id,
                        Body = body,
                        AuthorName = request.AuthorName,
                        AuthorEmail = request.AuthorEmail,
                        AuthorType = "Agent",
                        IsInternalNote = false,
                        UserId = request.UserId,
                        CreatedAt = DateTime.UtcNow,
                    }
                );
            }

            await _context.SaveChangesAsync();

            try
            {
                await _emailNotificationService.SendTicketClosedAsync(ticket, body);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to send closed notification for ticket {TicketId}.",
                    ticket.Id
                );
            }

            return await GetTicketByIdAsync(ticket.Id);
        }

        public async Task<TicketComment?> AddCommentAsync(
            int ticketId,
            CreateTicketCommentRequest request
        )
        {
            var ticketExists = await _context.Tickets.AnyAsync(ticket =>
                ticket.Id == ticketId && !ticket.IsDeleted
            );

            if (!ticketExists)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                throw new InvalidOperationException("Comment body is required.");
            }

            var comment = new TicketComment
            {
                TicketId = ticketId,
                Body = request.Body.Trim(),
                AuthorName = request.AuthorName,
                AuthorEmail = request.AuthorEmail,
                AuthorType = request.AuthorType,
                IsInternalNote = request.IsInternalNote,
                UserId = request.UserId,
                CreatedAt = DateTime.UtcNow,
            };

            _context.TicketComments.Add(comment);
            await _context.SaveChangesAsync();

            return comment;
        }

        public async Task<List<TicketComment>> GetCommentsByTicketIdAsync(int ticketId)
        {
            return await _context
                .TicketComments.AsNoTracking()
                .Where(comment => comment.TicketId == ticketId)
                .OrderBy(comment => comment.CreatedAt)
                .ToListAsync();
        }

        public async Task<TicketComment?> ReplyToRequesterAsync(
            int ticketId,
            CreateTicketReplyRequest request
        )
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(ticket =>
                ticket.Id == ticketId && !ticket.IsDeleted
            );

            if (ticket == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                throw new InvalidOperationException("Reply body is required.");
            }

            if (request.UserId.HasValue && ticket.UserId != request.UserId)
            {
                throw new UnauthorizedAccessException();
            }

            var user = await _context
                .Users.AsNoTracking()
                .FirstOrDefaultAsync(existingUser => existingUser.Id == request.UserId);

            var reply = new TicketComment
            {
                TicketId = ticketId,
                Body = request.Body.Trim(),
                AuthorName = !string.IsNullOrWhiteSpace(request.AuthorName)
                    ? request.AuthorName
                    : user?.Name,
                AuthorEmail = !string.IsNullOrWhiteSpace(request.AuthorEmail)
                    ? request.AuthorEmail
                    : user?.Email,
                AuthorType = "Agent",
                IsInternalNote = false,
                UserId = request.UserId,
                CreatedAt = DateTime.UtcNow,
            };

            _context.TicketComments.Add(reply);
            await _context.SaveChangesAsync();

            _backgroundEmailQueue.Enqueue(async (services, cancellationToken) =>
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                var emailNotificationService =
                    services.GetRequiredService<EmailNotificationService>();
                var logger = services.GetRequiredService<ILogger<TicketService>>();

                var ticketForEmail = await context
                    .Tickets.AsNoTracking()
                    .FirstOrDefaultAsync(
                        existingTicket => existingTicket.Id == ticketId && !existingTicket.IsDeleted,
                        cancellationToken
                    );
                var replyForEmail = await context
                    .TicketComments.AsNoTracking()
                    .FirstOrDefaultAsync(
                        existingReply => existingReply.Id == reply.Id,
                        cancellationToken
                    );

                if (ticketForEmail == null || replyForEmail == null)
                {
                    logger.LogWarning(
                        "Skipped requester reply email because ticket {TicketId} or reply {ReplyId} was not found.",
                        ticketId,
                        reply.Id
                    );
                    return;
                }

                try
                {
                    await emailNotificationService.SendTicketReplyAsync(ticketForEmail, replyForEmail);
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Failed to send requester reply notification for ticket {TicketId}.",
                        ticketId
                    );
                }
            });

            return reply;
        }

        public async Task<TicketListItemResponse?> CancelTicketAsync(int ticketId)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(ticket =>
                ticket.Id == ticketId && !ticket.IsDeleted
            );

            if (ticket == null)
            {
                return null;
            }

            var currentStatus = await _context
                .Statuses.AsNoTracking()
                .FirstOrDefaultAsync(status => status.Id == ticket.StatusId);

            if (currentStatus?.Name == "Closed")
            {
                throw new InvalidOperationException("Closed tickets cannot be cancelled.");
            }

            ticket.StatusId = await GetStatusIdByNameAsync("Cancelled");
            await _context.SaveChangesAsync();

            return await GetTicketByIdAsync(ticket.Id);
        }
    }
}
