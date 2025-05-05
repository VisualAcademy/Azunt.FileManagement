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
            modelBuilder.Entity<File>()
                .Property(m => m.Created)
                .HasDefaultValueSql("GetDate()");
        }

        public DbSet<File> Files { get; set; } = null!;
    }
}