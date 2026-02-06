using Microsoft.EntityFrameworkCore;
using URLShortener.API.Models;

namespace URLShortener.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UrlMapping> UrlMappings { get; set; }
        public DbSet<ClickLog> ClickLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.GoogleId).IsUnique();
                entity.HasIndex(e => e.Email);
            });

            // Configure UrlMapping
            modelBuilder.Entity<UrlMapping>(entity =>
            {
                entity.HasIndex(e => e.ShortCode).IsUnique();
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.UserId);

                entity.HasOne(u => u.User)
                      .WithMany(u => u.UrlMappings)
                      .HasForeignKey(u => u.UserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure ClickLog
            modelBuilder.Entity<ClickLog>(entity =>
            {
                entity.HasIndex(e => e.ClickedAt);
                entity.HasIndex(e => e.UrlMappingId);

                entity.HasOne(cl => cl.UrlMapping)
                      .WithMany(um => um.ClickLogs)
                      .HasForeignKey(cl => cl.UrlMappingId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}