using Microsoft.EntityFrameworkCore;
using PcBuilder.Core.Models;

namespace PcBuilder.Infrastructure.Data
{
    public class PcBuilderDbContext : DbContext
    {
        public PcBuilderDbContext(DbContextOptions<PcBuilderDbContext> options)
            : base(options) { }

        public DbSet<Part> Parts { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<SavedBuild> SavedBuilds { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(e =>
            {
                e.HasKey(u => u.Id);
                e.HasIndex(u => u.Email).IsUnique();
                e.Property(u => u.Email).HasMaxLength(200).IsRequired();
                e.Property(u => u.Username).HasMaxLength(100).IsRequired();
                e.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
            });

            modelBuilder.Entity<SavedBuild>(e =>
            {
                e.HasKey(b => b.Id);
                e.HasOne(b => b.User)
                 .WithMany(u => u.SavedBuilds)
                 .HasForeignKey(b => b.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.Property(b => b.Name).HasMaxLength(200);
                e.Property(b => b.Workload).HasMaxLength(50).IsRequired();
                e.Property(b => b.PartsJson).IsRequired();
            });
        }
    }
}