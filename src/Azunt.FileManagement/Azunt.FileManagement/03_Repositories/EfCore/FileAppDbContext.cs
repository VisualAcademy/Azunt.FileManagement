using Microsoft.EntityFrameworkCore;

namespace Azunt.FileManagement
{
    public class FileAppDbContext : DbContext
    {
        public FileAppDbContext(DbContextOptions<FileAppDbContext> options)
            : base(options)
        {
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FileEntity>()
                .Property(m => m.Created)
                .HasDefaultValueSql("GetDate()");
        }

        public DbSet<FileEntity> Files { get; set; } = null!;
    }
}