using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Azunt.FileManagement;

public class FileAppDbContextFactory
{
    private readonly IConfiguration? _configuration;

    public FileAppDbContextFactory() { }

    public FileAppDbContextFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public FileAppDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<FileAppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new FileAppDbContext(options);
    }

    public FileAppDbContext CreateDbContext(DbContextOptions<FileAppDbContext> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new FileAppDbContext(options);
    }

    public FileAppDbContext CreateDbContext()
    {
        if (_configuration == null)
        {
            throw new InvalidOperationException("Configuration is not provided.");
        }

        var defaultConnection = _configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(defaultConnection))
        {
            throw new InvalidOperationException("DefaultConnection is not configured properly.");
        }

        return CreateDbContext(defaultConnection);
    }
}