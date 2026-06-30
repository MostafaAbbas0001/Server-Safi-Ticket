using Microsoft.EntityFrameworkCore;
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
        private readonly TicketEventNotifier _ticketEventNotifier;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<TicketService> _logger;

        public TicketService(
            ApplicationDbContext context,
            EmailNotificationService emailNotificationService,
            BackgroundEmailQueue backgroundEmailQueue,
            TicketEventNotifier ticketEventNotifier,
            IWebHostEnvironment environment,
            ILogger<TicketService> logger
        )
        {
            _context = context;
            _emailNotificationService = emailNotificationService;
            _backgroundEmailQueue = backgroundEmailQueue;
            _ticketEventNotifier = ticketEventNotifier;
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
                RequesterEmail = requester.Contains("@") ? requester : string.Empty,
                PriorityId = null,
                StatusId = 1,
                UserId = null,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
            };

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            return ticket;
        }

        public async Task<List<TicketAttachment>> GetAttachmentsByTicketIdAsync(int ticketId)
        {
            return await _context
                .TicketAttachments.AsNoTracking()
                .Where(attachment => attachment.TicketId == ticketId)
                .OrderBy(attachment => attachment.UploadedAt)
                .ToListAsync();
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

        public async Task<List<object>> GetAllTicketsAsync()
        {
            var tickets = await _context
                .Tickets.AsNoTracking()
                .Include(ticket => ticket.Status)
                .Include(ticket => ticket.Priority)
                .Include(ticket => ticket.User)
                .Where(ticket => !ticket.IsDeleted)
                .OrderByDescending(ticket => ticket.CreatedAt)
                .Select(ticket => new
                {
                    ticket.Id,
                    ticket.Title,
                    ticket.Body,
                    ticket.Requester,
                    ticket.RequesterEmail,
                    ticket.StatusId,
                    Status = ticket.Status != null ? ticket.Status.Name : null,
                    ticket.PriorityId,
                    Priority = ticket.Priority != null ? ticket.Priority.Type : null,
                    ticket.UserId,
                    Assignee = ticket.User != null ? ticket.User.Name : null,
                    ticket.CreatedAt,
                    CommentCount = ticket.Comments.Count,
                    LastCommentAt = ticket
                        .Comments.OrderByDescending(comment => comment.CreatedAt)
                        .Select(comment => (DateTime?)comment.CreatedAt)
                        .FirstOrDefault(),
                })
                .ToListAsync();

            return tickets.Cast<object>().ToList();
        }

        public async Task<List<object>> GetAssignedTicketsAsync(int userId)
        {
            var tickets = await _context
                .Tickets.AsNoTracking()
                .Include(ticket => ticket.Status)
                .Include(ticket => ticket.Priority)
                .Include(ticket => ticket.User)
                .Where(ticket => !ticket.IsDeleted)
                .Where(ticket => ticket.UserId == userId)
                .OrderByDescending(ticket => ticket.CreatedAt)
                .Select(ticket => new
                {
                    ticket.Id,
                    ticket.Title,
                    ticket.Body,
                    ticket.Requester,
                    ticket.RequesterEmail,
                    ticket.StatusId,
                    Status = ticket.Status != null ? ticket.Status.Name : null,
                    ticket.PriorityId,
                    Priority = ticket.Priority != null ? ticket.Priority.Type : null,
                    ticket.UserId,
                    Assignee = ticket.User != null ? ticket.User.Name : null,
                    ticket.CreatedAt,
                    CommentCount = ticket.Comments.Count,
                    LastCommentAt = ticket
                        .Comments.OrderByDescending(comment => comment.CreatedAt)
                        .Select(comment => (DateTime?)comment.CreatedAt)
                        .FirstOrDefault(),
                })
                .ToListAsync();

            return tickets.Cast<object>().ToList();
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
                    PriorityId = ticket.PriorityId,
                    Priority = ticket.Priority != null ? ticket.Priority.Type : null,
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
            var pageSize = Math.Clamp(request.PageSize, 10, 100);
            var search = request.Search?.Trim();

            var baseQuery = _context.Tickets.AsNoTracking().Where(ticket => !ticket.IsDeleted);

            var statusCounts = await baseQuery
                .GroupBy(ticket => ticket.StatusId)
                .Select(group => new { StatusId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.StatusId, item => item.Count);

            var allCount = await baseQuery.CountAsync();
            var activeCount = await baseQuery.CountAsync(ticket =>
                ticket.Status != null
                && ticket.Status.Name != "Closed"
                && ticket.Status.Name != "Canceled"
                && ticket.Status.Name != "Cancelled"
            );
            var pendingCount = await baseQuery.CountAsync(ticket =>
                ticket.Status != null && ticket.Status.Name == "Pending"
            );
            var resolvedCount = await baseQuery.CountAsync(ticket =>
                ticket.Status != null && ticket.Status.Name == "Resolved"
            );
            var closedCount = await baseQuery.CountAsync(ticket =>
                ticket.Status != null && ticket.Status.Name == "Closed"
            );

            var filteredQuery = baseQuery;

            if (request.StatusId.HasValue)
            {
                filteredQuery = filteredQuery.Where(ticket =>
                    ticket.StatusId == request.StatusId.Value
                );
            }

            if (request.PriorityId.HasValue)
            {
                filteredQuery = filteredQuery.Where(ticket =>
                    ticket.PriorityId == request.PriorityId.Value
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
                    PriorityId = ticket.PriorityId,
                    Priority = ticket.Priority != null ? ticket.Priority.Type : null,
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
                AllCount = allCount,
                ActiveCount = activeCount,
                PendingCount = pendingCount,
                ResolvedCount = resolvedCount,
                ClosedCount = closedCount,
                StatusCounts = statusCounts,
            };
        }

        public async Task<string?> UpdateTicketAsync(int id, UpdateTicketRequest request)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(ticket =>
                ticket.Id == id && !ticket.IsDeleted
            );

            if (ticket == null)
            {
                return null;
            }

            var previousUserId = ticket.UserId;
            var previousStatusId = ticket.StatusId;

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                ticket.Title = request.Title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Body))
            {
                ticket.Body = request.Body.Trim();
            }

            if (request.StatusId.HasValue)
            {
                ticket.StatusId = request.StatusId.Value;
            }

            if (request.PriorityId.HasValue)
            {
                ticket.PriorityId = request.PriorityId.Value;
            }

            /*
             * Important:
             * IsUserIdSet lets admin unassign a ticket by sending:
             * { "userId": null }
             *
             * Without this flag, the backend cannot know the difference between:
             * - userId was not sent
             * - userId was sent as null
             */
            if (request.IsUserIdSet)
            {
                ticket.UserId = request.UserId;
            }

            await _context.SaveChangesAsync();

            if (request.StatusId.HasValue && request.StatusId.Value != previousStatusId)
            {
                var newStatus = await _context
                    .Statuses.AsNoTracking()
                    .FirstOrDefaultAsync(status => status.Id == request.StatusId.Value);

                if (newStatus?.Name == "Closed")
                {
                    try
                    {
                        await _emailNotificationService.SendTicketResolvedAsync(ticket);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(
                            exception,
                            "Failed to send resolved notification for ticket {TicketId}.",
                            ticket.Id
                        );
                    }
                }
            }

            if (request.IsUserIdSet && request.UserId.HasValue && previousUserId != request.UserId)
            {
                var assignee = await _context
                    .Users.AsNoTracking()
                    .FirstOrDefaultAsync(user => user.Id == request.UserId.Value);

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
                            "Failed to send assignment notification for ticket {TicketId} to user {UserId}.",
                            ticket.Id,
                            assignee.Id
                        );
                    }
                }
            }

            return "Ticket has been updated successfully.";
        }

        public async Task<string?> UserUpdateTicketAsync(
            int ticketId,
            UserTicketUpdateRequest request
        )
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(ticket =>
                ticket.Id == ticketId && !ticket.IsDeleted
            );

            if (ticket == null)
            {
                return null;
            }

            /*
             * Security rule:
             * A normal user/officer can update only tickets assigned to them.
             * They cannot update another user's ticket.
             * They also cannot assign/unassign tickets.
             */
            if (ticket.UserId != request.UserId)
            {
                throw new UnauthorizedAccessException();
            }

            var previousStatusId = ticket.StatusId;
            var shouldSendResolvedEmail = false;

            if (request.StatusId.HasValue)
            {
                var requestedStatus = await _context
                    .Statuses.AsNoTracking()
                    .FirstOrDefaultAsync(status => status.Id == request.StatusId.Value);

                var allowedUserStatuses = new[] { "Pending", "In Progress", "Resolved" };
                if (requestedStatus == null || !allowedUserStatuses.Contains(requestedStatus.Name))
                {
                    throw new InvalidOperationException(
                        "Staff can only set status to Pending, In Progress, or Resolved."
                    );
                }

                if (requestedStatus.Name == "Resolved")
                {
                    var closedStatus = await _context
                        .Statuses.AsNoTracking()
                        .FirstOrDefaultAsync(status => status.Name == "Closed");

                    if (closedStatus == null)
                    {
                        throw new InvalidOperationException("Closed status is missing from the database.");
                    }

                    ticket.StatusId = closedStatus.Id;
                    shouldSendResolvedEmail = previousStatusId != closedStatus.Id;
                }
                else
                {
                    ticket.StatusId = request.StatusId.Value;
                }
            }

            if (request.PriorityId.HasValue)
            {
                ticket.PriorityId = request.PriorityId.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.InternalNote))
            {
                var user = await _context
                    .Users.AsNoTracking()
                    .FirstOrDefaultAsync(existingUser => existingUser.Id == request.UserId);

                var internalNote = new TicketComment
                {
                    TicketId = ticket.Id,
                    Body = request.InternalNote.Trim(),
                    AuthorName = user?.Name ?? "User",
                    AuthorEmail = user?.Email,
                    AuthorType = "User",
                    IsInternalNote = true,
                    UserId = request.UserId,
                    CreatedAt = DateTime.UtcNow,
                };

                _context.TicketComments.Add(internalNote);
            }

            await _context.SaveChangesAsync();

            if (shouldSendResolvedEmail)
            {
                try
                {
                    await _emailNotificationService.SendTicketResolvedAsync(ticket);
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Failed to send resolved notification for ticket {TicketId}.",
                        ticket.Id
                    );
                }
            }

            if (request.StatusId.HasValue && ticket.StatusId != previousStatusId)
            {
                _ticketEventNotifier.Publish(
                    new TicketEvent(
                        "ticket-status-changed",
                        ticket.Id,
                        null,
                        $"TK-{ticket.Id:0000} status was updated.",
                        DateTime.UtcNow
                    )
                );
            }

            return "Ticket has been updated successfully.";
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

            _ticketEventNotifier.Publish(
                new TicketEvent(
                    "ticket-comment-added",
                    ticketId,
                    comment.Id,
                    $"New comment on TK-{ticketId:0000}",
                    comment.CreatedAt
                )
            );

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

            /*
             * If the reply is sent by an assigned user, make sure that user owns the ticket.
             * This prevents normal users from replying to tickets assigned to someone else.
             */
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

            _ticketEventNotifier.Publish(
                new TicketEvent(
                    "ticket-comment-added",
                    ticketId,
                    reply.Id,
                    $"New reply on TK-{ticketId:0000}",
                    reply.CreatedAt
                )
            );

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

        public async Task<Ticket> CreateTicketFromIncomingEmailAsync(
            TestIncomingEmailRequest request
        )
        {
            var subject = request.Subject.Trim();
            var body = request.Body.Trim();

            if (string.IsNullOrWhiteSpace(subject))
            {
                subject = "(No subject)";
            }

            var requester = !string.IsNullOrWhiteSpace(request.FromName)
                ? request.FromName.Trim()
                : request.FromEmail.Trim();

            var ticket = new Ticket
            {
                Title = subject,
                Body = body,
                Requester = requester,
                RequesterEmail = request.FromEmail.Trim(),
                StatusId = 1,
                PriorityId = null,
                UserId = null,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
            };

            var comment = new TicketComment
            {
                Ticket = ticket,
                Body = body,
                AuthorName = request.FromName,
                AuthorEmail = request.FromEmail,
                AuthorType = "Requester",
                IsInternalNote = false,
                UserId = null,
                CreatedAt = DateTime.UtcNow,
            };

            var emailMessage = new EmailMessage
            {
                Ticket = ticket,
                TicketComment = comment,
                MessageId = request.MessageId,
                FromEmail = request.FromEmail,
                FromName = request.FromName,
                Subject = subject,
                Body = body,
                ReceivedAt = request.ReceivedAt,
                ProcessedAt = DateTime.UtcNow,
            };

            _context.Tickets.Add(ticket);
            _context.TicketComments.Add(comment);
            _context.EmailMessages.Add(emailMessage);

            await _context.SaveChangesAsync();

            try
            {
                await _emailNotificationService.SendTicketReceivedAsync(ticket);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to send ticket received notification for test email ticket {TicketId}.",
                    ticket.Id
                );
            }

            return ticket;
        }

        public async Task<string?> DeleteTicket(int ticketId)
        {
            var ticket = await _context.Tickets.FirstOrDefaultAsync(ticket =>
                ticket.Id == ticketId && !ticket.IsDeleted
            );

            if (ticket == null)
            {
                return null;
            }

            ticket.IsDeleted = true;
            await _context.SaveChangesAsync();

            return "Ticket has been deleted successfully.";
        }
    }
}
