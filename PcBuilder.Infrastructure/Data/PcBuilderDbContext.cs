using Microsoft.EntityFrameworkCore;
using PcBuilder.Core.Models;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace PcBuilder.Infrastructure.Data
{
    public class PcBuilderDbContext : DbContext
    {
        public PcBuilderDbContext(DbContextOptions<PcBuilderDbContext> options)
            : base(options)
        {
        }

        public DbSet<Part> Parts => Set<Part>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Part>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Type)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(p => p.Name)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(p => p.Price)
                      .HasColumnType("decimal(18,2)");
            });
        }
    }
}
