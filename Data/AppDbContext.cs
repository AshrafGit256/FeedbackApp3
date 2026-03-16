using Microsoft.EntityFrameworkCore;
using FeedbackAPI.Models;

namespace FeedbackAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<EventModel> Events { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<EventModel>().ToTable("Events");
            modelBuilder.Entity<Feedback>().ToTable("Feedbacks");
            modelBuilder.Entity<AuditLog>().ToTable("AuditLogs");

            // Prevent SQL injection via max length constraints at DB level
            modelBuilder.Entity<Feedback>()
                .Property(f => f.Category)
                .HasMaxLength(50);

            modelBuilder.Entity<Feedback>()
                .Property(f => f.Message)
                .HasMaxLength(1000);

            modelBuilder.Entity<Feedback>()
                .Property(f => f.Status)
                .HasMaxLength(20);

            modelBuilder.Entity<Feedback>()
                .HasOne<EventModel>()
                .WithMany()
                .HasForeignKey(f => f.Event_id)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Action)
                .HasMaxLength(100);

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Entity)
                .HasMaxLength(100);

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.IpAddress)
                .HasMaxLength(45);
        }
    }
}