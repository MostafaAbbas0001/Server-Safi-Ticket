using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Models;

namespace Safi_Ticket.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }

        public DbSet<Role> Roles { get; set; }

        public DbSet<Ticket> Tickets { get; set; }

        public DbSet<Status> Statuses { get; set; }

        public DbSet<Priority> Priorities { get; set; }

        public DbSet<EmailMessage> EmailMessages { get; set; }

        public DbSet<TicketComment> TicketComments { get; set; }

        public DbSet<TicketAttachment> TicketAttachments { get; set; }

        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder
                .Entity<User>()
                .HasOne(user => user.Role)
                .WithMany(role => role.Users)
                .HasForeignKey(user => user.RoleId);

            modelBuilder
                .Entity<Ticket>()
                .HasOne(ticket => ticket.User)
                .WithMany(user => user.Tickets)
                .HasForeignKey(ticket => ticket.UserId);

            modelBuilder
                .Entity<Ticket>()
                .HasOne(ticket => ticket.Status)
                .WithMany(status => status.Tickets)
                .HasForeignKey(ticket => ticket.StatusId);

            modelBuilder
                .Entity<Ticket>()
                .HasOne(ticket => ticket.Priority)
                .WithMany(priority => priority.Tickets)
                .HasForeignKey(ticket => ticket.PriorityId);

            modelBuilder.Entity<Ticket>().HasIndex(ticket => new { ticket.IsDeleted, ticket.CreatedAt });

            modelBuilder.Entity<Ticket>().HasIndex(ticket => new { ticket.IsDeleted, ticket.StatusId });

            modelBuilder.Entity<Ticket>().HasIndex(ticket => new { ticket.IsDeleted, ticket.PriorityId });

            modelBuilder.Entity<Ticket>().HasIndex(ticket => new { ticket.IsDeleted, ticket.UserId });

            modelBuilder.Entity<Ticket>().HasIndex(ticket => ticket.RequesterEmail);

            modelBuilder.Entity<EmailMessage>().HasIndex(e => e.MessageId).IsUnique();

            modelBuilder
                .Entity<EmailMessage>()
                .HasOne(e => e.Ticket)
                .WithMany(t => t.EmailMessages)
                .HasForeignKey(e => e.TicketId);

            modelBuilder
                .Entity<EmailMessage>()
                .HasOne(emailMessage => emailMessage.TicketComment)
                .WithMany()
                .HasForeignKey(emailMessage => emailMessage.TicketCommentId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder
                .Entity<TicketComment>()
                .HasOne(comment => comment.Ticket)
                .WithMany(ticket => ticket.Comments)
                .HasForeignKey(comment => comment.TicketId);

            modelBuilder
                .Entity<TicketComment>()
                .HasOne(comment => comment.User)
                .WithMany()
                .HasForeignKey(comment => comment.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<TicketComment>().HasIndex(comment => new { comment.TicketId, comment.CreatedAt });

            modelBuilder
                .Entity<TicketAttachment>()
                .HasOne(attachment => attachment.Ticket)
                .WithMany(ticket => ticket.Attachments)
                .HasForeignKey(attachment => attachment.TicketId);

            modelBuilder.Entity<TicketAttachment>().HasIndex(attachment => attachment.TicketId);

            modelBuilder
                .Entity<PasswordResetToken>()
                .HasOne(token => token.User)
                .WithMany()
                .HasForeignKey(token => token.UserId);

            modelBuilder.Entity<PasswordResetToken>().HasIndex(token => token.TokenHash).IsUnique();
        }
    }
}
