using DocQA.Models;
using Microsoft.EntityFrameworkCore;

namespace DocQA.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<Chunk> Chunks => Set<Chunk>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Document>(e =>
            {
                e.Property(x => x.FileName).HasMaxLength(260);
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                e.HasMany(x => x.Chunks)
                    .WithOne(x => x.Document)
                    .HasForeignKey(x => x.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Chunk>()
                .HasIndex(x => new { x.DocumentId, x.Index })
                .IsUnique();
        }
    }
}
