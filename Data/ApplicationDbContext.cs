using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Safi_Ticket.Models;

namespace Safi_Ticket.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<Status> Statuses => Set<Status>();

    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

    public DbSet<TicketComment> TicketComments => Set<TicketComment>();

    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();

    public DbSet<TicketReplyReadState> TicketReplyReadStates => Set<TicketReplyReadState>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(user =>
        {
            user.HasOne(u => u.Role).WithMany(role => role.Users).HasForeignKey(u => u.RoleId);
        });

        modelBuilder.Entity<Ticket>(ticket =>
        {
            ticket.HasOne(t => t.User).WithMany(user => user.Tickets).HasForeignKey(t => t.UserId);
            ticket
                .HasOne(t => t.Status)
                .WithMany(status => status.Tickets)
                .HasForeignKey(t => t.StatusId);
            ticket.HasIndex(t => new { t.IsDeleted, t.CreatedAt });
            ticket.HasIndex(t => new { t.IsDeleted, t.StatusId });
            ticket.HasIndex(t => new { t.IsDeleted, t.UserId });
            ticket.HasIndex(t => t.RequesterEmail);
        });

        modelBuilder.Entity<EmailMessage>(email =>
        {
            email.HasIndex(e => e.MessageId).IsUnique();
            email
                .HasOne(e => e.Ticket)
                .WithMany(t => t.EmailMessages)
                .HasForeignKey(e => e.TicketId);
            email
                .HasOne(e => e.TicketComment)
                .WithMany()
                .HasForeignKey(e => e.TicketCommentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TicketComment>(comment =>
        {
            comment
                .HasOne(c => c.Ticket)
                .WithMany(ticket => ticket.Comments)
                .HasForeignKey(c => c.TicketId);
            comment
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            comment.HasIndex(c => new { c.TicketId, c.CreatedAt });
            comment.Ignore(c => c.IsFromRequesterSide);
            MapParticipants(comment.Property(c => c.ToRecipients));
            MapParticipants(comment.Property(c => c.CcRecipients));
        });

        modelBuilder.Entity<TicketAttachment>(attachment =>
        {
            attachment
                .HasOne(a => a.Ticket)
                .WithMany(ticket => ticket.Attachments)
                .HasForeignKey(a => a.TicketId);
            attachment.HasIndex(a => a.TicketId);
        });

        modelBuilder.Entity<TicketReplyReadState>(readState =>
        {
            readState.HasKey(r => new { r.UserId, r.TicketId });
            readState
                .HasOne(r => r.User)
                .WithMany(user => user.TicketReplyReadStates)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            readState
                .HasOne(r => r.Ticket)
                .WithMany(ticket => ticket.ReplyReadStates)
                .HasForeignKey(r => r.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            readState.HasIndex(r => r.TicketId);
        });

        modelBuilder.Entity<PasswordResetToken>(token =>
        {
            token.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId);
            token.HasIndex(t => t.TokenHash).IsUnique();
        });
    }

    private static readonly JsonSerializerOptions ParticipantJson = new(JsonSerializerDefaults.Web);

    /// <summary>Stores a participant list as a jsonb array, e.g. [{"name":"Hazem","email":"h@x.com"}].</summary>
    private static void MapParticipants(PropertyBuilder<List<EmailParticipant>> property)
    {
        property
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .HasConversion(
                list => JsonSerializer.Serialize(list, ParticipantJson),
                json =>
                    JsonSerializer.Deserialize<List<EmailParticipant>>(json, ParticipantJson)
                    ?? new()
            )
            .Metadata.SetValueComparer(
                new ValueComparer<List<EmailParticipant>>(
                    (left, right) =>
                        left == null ? right == null : right != null && left.SequenceEqual(right),
                    list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                    list => list.ToList()
                )
            );
    }
}
