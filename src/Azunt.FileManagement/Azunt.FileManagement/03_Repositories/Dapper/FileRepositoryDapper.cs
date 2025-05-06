using Dapper;
using Dul.Articles;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Azunt.FileManagement;

public class FileRepositoryDapper : IFileRepository
{
    private readonly string _connectionString;
    private readonly ILogger<FileRepositoryDapper> _logger;

    public FileRepositoryDapper(string connectionString, ILoggerFactory loggerFactory)
    {
        _connectionString = connectionString;
        _logger = loggerFactory.CreateLogger<FileRepositoryDapper>();
    }

    private SqlConnection GetConnection() => new(_connectionString);

    public async Task<FileEntity> AddAsync(FileEntity model)
    {
        const string sql = @"
            INSERT INTO Files (Active, Created, CreatedBy, Name, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES (@Active, @Created, @CreatedBy, @Name, 0)";

        model.Created = DateTimeOffset.UtcNow;

        using var conn = GetConnection();
        model.Id = await conn.ExecuteScalarAsync<long>(sql, model);
        return model;
    }

    public async Task<IEnumerable<FileEntity>> GetAllAsync()
    {
        const string sql = @"
            SELECT Id, Active, Created, CreatedBy, Name 
            FROM Files 
            WHERE IsDeleted = 0 
            ORDER BY Id DESC";

        using var conn = GetConnection();
        return await conn.QueryAsync<FileEntity>(sql);
    }

    public async Task<FileEntity> GetByIdAsync(long id)
    {
        const string sql = @"
            SELECT Id, Active, Created, CreatedBy, Name 
            FROM Files 
            WHERE Id = @Id AND IsDeleted = 0";

        using var conn = GetConnection();
        return await conn.QuerySingleOrDefaultAsync<FileEntity>(sql, new { Id = id }) ?? new FileEntity();
    }

    public async Task<bool> UpdateAsync(FileEntity model)
    {
        const string sql = @"
            UPDATE Files SET
                Active = @Active,
                Name = @Name
            WHERE Id = @Id AND IsDeleted = 0";

        using var conn = GetConnection();
        var affected = await conn.ExecuteAsync(sql, model);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        const string sql = @"
            UPDATE Files SET IsDeleted = 1 
            WHERE Id = @Id AND IsDeleted = 0";

        using var conn = GetConnection();
        var affected = await conn.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<Azunt.Models.Common.ArticleSet<FileEntity, int>> GetAllAsync<TParentIdentifier>(
        int pageIndex, int pageSize, string searchField, string searchQuery, string sortOrder, TParentIdentifier parentIdentifier)
    {
        var all = await GetAllAsync();
        var filtered = string.IsNullOrWhiteSpace(searchQuery)
            ? all
            : all.Where(m => m.Name != null && m.Name.Contains(searchQuery)).ToList();

        var paged = filtered
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();

        return new Azunt.Models.Common.ArticleSet<FileEntity, int>(paged, filtered.Count());
    }

    public async Task<Azunt.Models.Common.ArticleSet<FileEntity, long>> GetAllAsync<TParentIdentifier>(FilterOptions<TParentIdentifier> options)
    {
        var all = await GetAllAsync();
        var filtered = all
            .Where(m => string.IsNullOrWhiteSpace(options.SearchQuery)
                     || (m.Name != null && m.Name.Contains(options.SearchQuery)))
            .ToList();

        var paged = filtered
            .Skip(options.PageIndex * options.PageSize)
            .Take(options.PageSize)
            .ToList();

        return new Azunt.Models.Common.ArticleSet<FileEntity, long>(paged, filtered.Count);
    }

    public async Task<bool> MoveUpAsync(long id)
    {
        const string getCurrent = "SELECT Id, DisplayOrder FROM Files WHERE Id = @Id AND IsDeleted = 0";
        const string getUpper = @"
        SELECT TOP 1 Id, DisplayOrder 
        FROM Files 
        WHERE DisplayOrder < @DisplayOrder AND IsDeleted = 0 
        ORDER BY DisplayOrder DESC";
        const string update = "UPDATE Files SET DisplayOrder = @DisplayOrder WHERE Id = @Id";

        using var conn = GetConnection();
        await conn.OpenAsync();

        var current = await conn.QuerySingleOrDefaultAsync<(long Id, int DisplayOrder)>(getCurrent, new { Id = id });
        if (current.Id == 0) return false;

        var upper = await conn.QuerySingleOrDefaultAsync<(long Id, int DisplayOrder)>(getUpper, new { DisplayOrder = current.DisplayOrder });
        if (upper.Id == 0) return false;

        using var tx = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(update, new { DisplayOrder = upper.DisplayOrder, Id = current.Id }, tx);
            await conn.ExecuteAsync(update, new { DisplayOrder = current.DisplayOrder, Id = upper.Id }, tx);
            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            return false;
        }
    }

    public async Task<bool> MoveDownAsync(long id)
    {
        const string getCurrent = "SELECT Id, DisplayOrder FROM Files WHERE Id = @Id AND IsDeleted = 0";
        const string getLower = @"
        SELECT TOP 1 Id, DisplayOrder 
        FROM Files 
        WHERE DisplayOrder > @DisplayOrder AND IsDeleted = 0 
        ORDER BY DisplayOrder ASC";
        const string update = "UPDATE Files SET DisplayOrder = @DisplayOrder WHERE Id = @Id";

        using var conn = GetConnection();
        await conn.OpenAsync();

        var current = await conn.QuerySingleOrDefaultAsync<(long Id, int DisplayOrder)>(getCurrent, new { Id = id });
        if (current.Id == 0) return false;

        var lower = await conn.QuerySingleOrDefaultAsync<(long Id, int DisplayOrder)>(getLower, new { DisplayOrder = current.DisplayOrder });
        if (lower.Id == 0) return false;

        using var tx = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(update, new { DisplayOrder = lower.DisplayOrder, Id = current.Id }, tx);
            await conn.ExecuteAsync(update, new { DisplayOrder = current.DisplayOrder, Id = lower.Id }, tx);
            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            return false;
        }
    }
}