using DocQA.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DocQA.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
    {
        public const int EmbeddingDimensions = 768;

        public DbSet<Document> Documents => Set<Document>();
        public DbSet<Chunk> Chunks => Set<Chunk>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // creates the Identity tables. Keep this first.

            modelBuilder.Entity<Document>(e =>
            {
                e.Property(x => x.FileName).HasMaxLength(260);
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                e.HasIndex(x => x.UserId);

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasMany(x => x.Chunks)
                    .WithOne(x => x.Document)
                    .HasForeignKey(x => x.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Chunk>(e =>
            {
                e.Property(x => x.Embedding).HasColumnType("varbinary(max)");
                e.HasIndex(x => new { x.DocumentId, x.Index }).IsUnique();
            });
        }

    }
}


